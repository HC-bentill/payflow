using MediatR;
using Microsoft.Extensions.Logging;
using PayFlow.Application.Common;
using PayFlow.Application.Common.Exceptions;
using PayFlow.Application.Common.Observability;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;
using PayFlow.Domain.Events;
using PayFlow.Domain.Exceptions;
using PayFlow.Domain.Interfaces;

namespace PayFlow.Application.Wallets.Commands;

public sealed class TopUpWalletHandler(
    IWalletRepository walletRepository,
    ITopUpRepository topUpRepository,
    ILedgerRepository ledgerRepository,
    IIdempotencyService idempotencyService,
    IUnitOfWork unitOfWork,
    IEventPublisher eventPublisher,
    ITenantContext tenantContext,
    IPayFlowMetrics metrics,
    ILogger<TopUpWalletHandler> logger)
    : IRequestHandler<TopUpWalletCommand, TopUpWalletResult>
{
    private const string WalletEventsTopic = "wallet.events";

    public async Task<TopUpWalletResult> Handle(TopUpWalletCommand request, CancellationToken cancellationToken)
    {
        var tenant = tenantContext.CurrentTenant;
        var tier = tenant.Tier.ToString();
        var cacheScope = $"topup:{request.TenantId}:{request.WalletId}";
        var lockScope = $"topup:{request.TenantId}";

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["TenantId"] = request.TenantId,
            ["WalletId"] = request.WalletId,
            ["IdempotencyKey"] = request.IdempotencyKey
        });

        var cached = await idempotencyService.GetCachedResponseAsync<TopUpWalletResult>(
            cacheScope,
            request.IdempotencyKey,
            cancellationToken);

        if (cached is not null)
        {
            metrics.RecordIdempotencyReplay(tier);
            return cached with { IsReplay = true };
        }

        if (!await idempotencyService.AcquireLockAsync(lockScope, request.IdempotencyKey, cancellationToken))
        {
            throw new PaymentAlreadyProcessingException();
        }

        try
        {
            var existing = await topUpRepository.GetByIdempotencyKeyAsync(
                request.WalletId,
                request.IdempotencyKey,
                cancellationToken);

            if (existing is not null)
            {
                var existingBalance = await ledgerRepository.GetWalletBalanceAsync(request.WalletId, cancellationToken);
                var existingResult = new TopUpWalletResult(
                    existing.Id,
                    existing.WalletId,
                    existing.Amount,
                    existing.Currency,
                    existingBalance,
                    false,
                    existing.CreatedAt);

                await idempotencyService.CacheResponseAsync(
                    cacheScope,
                    request.IdempotencyKey,
                    existingResult,
                    cancellationToken);

                metrics.RecordIdempotencyReplay(tier);
                return existingResult with { IsReplay = true };
            }

            var wallet = await walletRepository.GetByIdAsync(request.WalletId, cancellationToken);
            if (wallet is null)
            {
                throw new NotFoundException("Wallet", request.WalletId);
            }

            if (wallet.OwnerId != request.TenantId)
            {
                throw new UnauthorizedException("You can only top up your own wallets", 403);
            }

            if (!string.Equals(wallet.Currency, request.Currency, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Top-up currency {request.Currency} does not match wallet currency {wallet.Currency}");
            }

            var now = DateTime.UtcNow;
            var topUp = new TopUp(
                Guid.NewGuid(),
                request.WalletId,
                request.TenantId,
                request.IdempotencyKey,
                request.Amount,
                request.Currency,
                TopUpStatus.Pending,
                now,
                now);
            var ledgerEntry = new LedgerEntry(
                Guid.NewGuid(),
                topUp.Id,
                request.WalletId,
                request.TenantId,
                LedgerEntryType.Credit,
                request.Amount,
                request.Currency,
                now,
                LedgerEntrySource.TopUp);

            await using (var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken))
            {
                try
                {
                    await topUpRepository.AddAsync(topUp, cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);

                    await ledgerRepository.AddRangeAsync([ledgerEntry], cancellationToken);
                    topUp.MarkCompleted(DateTime.UtcNow);
                    await unitOfWork.SaveChangesAsync(cancellationToken);

                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }

            var newBalance = await ledgerRepository.GetWalletBalanceAsync(request.WalletId, cancellationToken);
            var result = new TopUpWalletResult(
                topUp.Id,
                topUp.WalletId,
                topUp.Amount,
                topUp.Currency,
                newBalance,
                false,
                topUp.CreatedAt);

            await PublishWalletToppedUpAsync(topUp, newBalance, cancellationToken);
            metrics.RecordTopUp(topUp.Currency, tier);
            logger.LogInformation("Top-up {TopUpId} completed for wallet {WalletId}", topUp.Id, topUp.WalletId);

            await idempotencyService.CacheResponseAsync(
                cacheScope,
                request.IdempotencyKey,
                result,
                cancellationToken);

            return result;
        }
        finally
        {
            await idempotencyService.ReleaseLockAsync(lockScope, request.IdempotencyKey, cancellationToken);
        }
    }

    private async Task PublishWalletToppedUpAsync(TopUp topUp, decimal newBalance, CancellationToken cancellationToken)
    {
        try
        {
            var @event = new WalletToppedUpEvent(
                topUp.Id,
                topUp.WalletId,
                topUp.TenantId,
                topUp.Amount,
                topUp.Currency,
                newBalance,
                DateTime.UtcNow);

            await eventPublisher.PublishAsync(WalletEventsTopic, @event, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to publish wallet topped-up event; top-up will remain completed");
        }
    }
}
