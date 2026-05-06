using System.Text.RegularExpressions;
using MediatR;
using Microsoft.Extensions.Logging;
using PayFlow.Application.Common;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Events;
using PayFlow.Domain.Exceptions;
using PayFlow.Domain.Interfaces;

namespace PayFlow.Application.Payments.Commands;

public sealed partial class CreatePaymentHandler(
    IPaymentRepository paymentRepository,
    ILedgerRepository ledgerRepository,
    IIdempotencyService idempotencyService,
    IEventPublisher eventPublisher,
    IUnitOfWork unitOfWork,
    ILogger<CreatePaymentHandler> logger)
    : IRequestHandler<CreatePaymentCommand, CreatePaymentResult>
{
    private const string PaymentEventsTopic = "payment.events";

    public async Task<CreatePaymentResult> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        if (!CurrencyRegex().IsMatch(request.Currency))
        {
            throw new InvalidCurrencyException();
        }

        var tenantId = request.TenantId.ToString();
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["TenantId"] = request.TenantId,
            ["IdempotencyKey"] = request.IdempotencyKey
        });

        logger.LogInformation("Checking idempotency cache for payment request");
        var cached = await idempotencyService.GetCachedResponseAsync<CreatePaymentResult>(
            tenantId,
            request.IdempotencyKey,
            cancellationToken);

        if (cached is not null)
        {
            logger.LogInformation("Returning cached idempotent payment response for payment {PaymentId}", cached.PaymentId);
            return cached with { IsReplay = true };
        }

        logger.LogInformation("Acquiring idempotency lock for payment request");
        if (!await idempotencyService.AcquireLockAsync(tenantId, request.IdempotencyKey, cancellationToken))
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
                logger.LogInformation(
                    "Returning existing idempotent payment response for payment {PaymentId}",
                    existing.Id);

                var existingResult = ToResult(existing, isReplay: false);
                await idempotencyService.CacheResponseAsync(
                    tenantId,
                    request.IdempotencyKey,
                    existingResult,
                    cancellationToken);

                return existingResult with { IsReplay = true };
            }

            var paymentId = Guid.NewGuid();
            using var paymentScope = logger.BeginScope(new Dictionary<string, object?>
            {
                ["PaymentId"] = paymentId
            });

            var now = DateTime.UtcNow;
            var payment = new Payment(
                paymentId,
                request.TenantId,
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
                    request.TenantId,
                    LedgerEntryType.Debit,
                    request.Amount,
                    request.Currency,
                    now),
                new LedgerEntry(
                    Guid.NewGuid(),
                    paymentId,
                    request.TenantId,
                    LedgerEntryType.Credit,
                    request.Amount,
                    request.Currency,
                    now)
            };

            logger.LogInformation("Beginning payment and ledger transaction");
            await using (var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken))
            {
                try
                {
                    await paymentRepository.AddAsync(payment, cancellationToken);
                    await ledgerRepository.AddRangeAsync(ledgerEntries, cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    logger.LogInformation("Committed payment and ledger transaction");
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    logger.LogWarning("Rolled back payment and ledger transaction");
                    throw;
                }
            }

            await PublishPaymentProcessedAsync(payment, cancellationToken);

            payment.MarkSucceeded(DateTime.UtcNow);
            await paymentRepository.UpdateAsync(payment, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Payment status updated to {PaymentStatus}", payment.Status);

            var result = ToResult(payment, isReplay: false);
            await idempotencyService.CacheResponseAsync(
                tenantId,
                request.IdempotencyKey,
                result,
                cancellationToken);
            logger.LogInformation("Cached idempotent payment response");

            return result;
        }
        finally
        {
            await idempotencyService.ReleaseLockAsync(tenantId, request.IdempotencyKey, cancellationToken);
            logger.LogInformation("Released idempotency lock");
        }
    }

    private async Task PublishPaymentProcessedAsync(Payment payment, CancellationToken cancellationToken)
    {
        try
        {
            var @event = new PaymentProcessedEvent(
                payment.Id,
                payment.TenantId,
                payment.Amount,
                payment.Currency,
                PaymentStatus.Succeeded,
                payment.IdempotencyKey,
                DateTime.UtcNow);

            await eventPublisher.PublishAsync(PaymentEventsTopic, @event, cancellationToken);
            logger.LogInformation("Published payment processed event");
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to publish payment processed event; payment will remain successful");
        }
    }

    private static CreatePaymentResult ToResult(Payment payment, bool isReplay)
    {
        return new CreatePaymentResult(
            payment.Id,
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
