using System.Collections.Concurrent;
using System.Text.RegularExpressions;
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

namespace PayFlow.Application.Payments.Commands;

public sealed partial class CreatePaymentHandler(
    IPaymentRepository paymentRepository,
    ILedgerRepository ledgerRepository,
    IWalletRepository walletRepository,
    ITenantRepository tenantRepository,
    IIdempotencyService idempotencyService,
    IEventPublisher eventPublisher,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    IPayFlowMetrics metrics,
    ILogger<CreatePaymentHandler> logger)
    : IRequestHandler<CreatePaymentCommand, CreatePaymentResult>
{
    private const string PaymentEventsTopic = "payment.events";
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> SenderWalletLocks = [];

    public async Task<CreatePaymentResult> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        var tenant = tenantContext.CurrentTenant;
        var tier = tenant.Tier.ToString();
        var tenantIdString = request.TenantId.ToString();
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["TenantId"] = request.TenantId,
            ["ReceiverTenantId"] = request.ReceiverTenantId,
            ["IdempotencyKey"] = request.IdempotencyKey
        });
        var recordedPaymentOutcome = false;

        try
        {
            if (!CurrencyRegex().IsMatch(request.Currency))
            {
                throw new InvalidCurrencyException();
            }

            var cached = await idempotencyService.GetCachedResponseAsync<CreatePaymentResult>(
                tenantIdString,
                request.IdempotencyKey,
                cancellationToken);

            if (cached is not null)
            {
                metrics.RecordIdempotencyReplay(tier);
                return cached with { IsReplay = true };
            }

            if (!await idempotencyService.AcquireLockAsync(tenantIdString, request.IdempotencyKey, cancellationToken))
            {
                throw new PaymentAlreadyProcessingException();
            }

            try
            {
                var existing = await paymentRepository.GetByIdempotencyKeyAsync(
                    request.TenantId,
                    request.IdempotencyKey,
                    cancellationToken);

                if (existing is not null)
                {
                    var existingResult = ToResult(existing, isReplay: false);
                    await idempotencyService.CacheResponseAsync(
                        tenantIdString,
                        request.IdempotencyKey,
                        existingResult,
                        cancellationToken);

                    metrics.RecordIdempotencyReplay(tier);
                    return existingResult with { IsReplay = true };
                }

                if (request.TenantId == request.ReceiverTenantId)
                {
                    throw new InvalidOperationException("Sender and receiver cannot be the same tenant");
                }

                var receiverTenant = await tenantRepository.GetByIdAsync(request.ReceiverTenantId, cancellationToken);
                if (receiverTenant is null)
                {
                    throw new NotFoundException("Tenant", request.ReceiverTenantId);
                }

                var senderWallet = await walletRepository.FindOrCreateAsync(
                    request.TenantId,
                    request.Currency,
                    cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                var senderBalance = await ledgerRepository.GetWalletBalanceAsync(senderWallet.Id, cancellationToken);
                if (senderBalance < request.Amount)
                {
                    throw new InsufficientFundsException(
                        senderWallet.Id,
                        request.Amount,
                        senderBalance,
                        request.Currency);
                }

                var paymentId = Guid.NewGuid();
                var now = DateTime.UtcNow;
                Payment payment;
                var senderWalletLock = SenderWalletLocks.GetOrAdd(senderWallet.Id, _ => new SemaphoreSlim(1, 1));

                await senderWalletLock.WaitAsync(cancellationToken);
                try
                {
                    await using (var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken))
                    {
                        try
                        {
                            var lockedSenderWallet = await walletRepository.GetByIdWithLockAsync(
                                senderWallet.Id,
                                cancellationToken);
                            if (lockedSenderWallet is null)
                            {
                                throw new NotFoundException("Wallet", senderWallet.Id);
                            }

                            var lockedSenderBalance = await ledgerRepository.GetWalletBalanceAsync(
                                lockedSenderWallet.Id,
                                cancellationToken);
                            if (lockedSenderBalance < request.Amount)
                            {
                                throw new InsufficientFundsException(
                                    lockedSenderWallet.Id,
                                    request.Amount,
                                    lockedSenderBalance,
                                    request.Currency);
                            }

                            var receiverWallet = await walletRepository.FindOrCreateAsync(
                                request.ReceiverTenantId,
                                request.Currency,
                                cancellationToken);

                            payment = new Payment(
                                paymentId,
                                request.TenantId,
                                lockedSenderWallet.Id,
                                receiverWallet.Id,
                                request.TenantId,
                                request.ReceiverTenantId,
                                request.IdempotencyKey,
                                request.Amount,
                                request.Currency,
                                PaymentStatus.Processing,
                                request.Description,
                                request.Metadata,
                                now,
                                now);

                            var ledgerEntries = new[]
                            {
                                new LedgerEntry(
                                    Guid.NewGuid(),
                                    paymentId,
                                    lockedSenderWallet.Id,
                                    request.TenantId,
                                    LedgerEntryType.Debit,
                                    request.Amount,
                                    request.Currency,
                                    now,
                                    LedgerEntrySource.Payment),
                                new LedgerEntry(
                                    Guid.NewGuid(),
                                    paymentId,
                                    receiverWallet.Id,
                                    request.ReceiverTenantId,
                                    LedgerEntryType.Credit,
                                    request.Amount,
                                    request.Currency,
                                    now,
                                    LedgerEntrySource.Payment)
                            };

                            await paymentRepository.AddAsync(payment, cancellationToken);
                            await ledgerRepository.AddRangeAsync(ledgerEntries, cancellationToken);
                            await unitOfWork.SaveChangesAsync(cancellationToken);
                            await transaction.CommitAsync(cancellationToken);
                        }
                        catch
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            throw;
                        }
                    }
                }
                finally
                {
                    senderWalletLock.Release();
                }

                await PublishPaymentProcessedAsync(payment, cancellationToken);

                payment.MarkSucceeded(DateTime.UtcNow);
                await paymentRepository.UpdateAsync(payment, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                metrics.RecordPayment("succeeded", payment.Currency, tier, payment.Amount, "debit");
                metrics.RecordPayment("succeeded", payment.Currency, tier, payment.Amount, "credit");
                recordedPaymentOutcome = true;
                logger.LogInformation("Payment {PaymentId} succeeded for tenant {TenantId}", payment.Id, payment.TenantId);

                var result = ToResult(payment, isReplay: false);
                await idempotencyService.CacheResponseAsync(
                    tenantIdString,
                    request.IdempotencyKey,
                    result,
                    cancellationToken);

                return result;
            }
            finally
            {
                await idempotencyService.ReleaseLockAsync(tenantIdString, request.IdempotencyKey, cancellationToken);
            }
        }
        catch (InsufficientFundsException)
        {
            metrics.RecordInsufficientFunds(request.Currency, tier);
            if (!recordedPaymentOutcome)
            {
                metrics.RecordPayment("failed", request.Currency, tier, request.Amount);
            }

            throw;
        }
        catch
        {
            if (!recordedPaymentOutcome)
            {
                metrics.RecordPayment("failed", request.Currency, tier, request.Amount);
            }

            throw;
        }
    }

    private async Task PublishPaymentProcessedAsync(Payment payment, CancellationToken cancellationToken)
    {
        try
        {
            var @event = new PaymentProcessedEvent(
                payment.Id,
                payment.TenantId,
                payment.SenderWalletId,
                payment.ReceiverWalletId,
                payment.SenderTenantId,
                payment.ReceiverTenantId,
                payment.Amount,
                payment.Currency,
                PaymentStatus.Succeeded,
                payment.IdempotencyKey,
                DateTime.UtcNow);

            await eventPublisher.PublishAsync(PaymentEventsTopic, @event, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to publish payment processed event; payment will remain successful");
        }
    }

    private static CreatePaymentResult ToResult(Payment payment, bool isReplay)
    {
        return new CreatePaymentResult(
            payment.Id,
            payment.SenderWalletId,
            payment.ReceiverWalletId,
            payment.SenderTenantId,
            payment.ReceiverTenantId,
            payment.IdempotencyKey,
            payment.Amount,
            payment.Currency,
            payment.Status,
            payment.CreatedAt,
            isReplay)
        {
            Description = payment.Description
        };
    }

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyRegex();
}
