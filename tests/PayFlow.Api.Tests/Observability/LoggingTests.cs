using FluentAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using PayFlow.Application.Common;
using PayFlow.Application.Common.Observability;
using PayFlow.Application.Payments.Commands;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Events;
using PayFlow.Domain.Interfaces;

namespace PayFlow.Api.Tests.Observability;

public sealed class LoggingTests
{
    private const string PaymentSucceededTemplate = "Payment {PaymentId} succeeded for tenant {TenantId}";

    [Fact]
    public async Task CreatePaymentHandler_LogsPaymentSuccessWithStructuredPaymentAndTenantProperties()
    {
        var senderTenantId = Guid.NewGuid();
        var receiverTenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var tenant = new Tenant(senderTenantId, "tenant", "hash", TenantTier.Pro, now, true);
        var receiver = new Tenant(receiverTenantId, "receiver", "hash2", TenantTier.Free, now, true);

        var paymentRepository = new Mock<IPaymentRepository>();
        var ledgerRepository = new Mock<ILedgerRepository>();
        var walletRepository = new Mock<IWalletRepository>();
        var tenantRepository = new Mock<ITenantRepository>();
        var idempotencyService = new Mock<IIdempotencyService>();
        var eventPublisher = new Mock<IEventPublisher>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var transaction = new Mock<IDbContextTransaction>();
        var tenantContext = new Mock<ITenantContext>();
        var metrics = new Mock<IPayFlowMetrics>();
        var logger = new Mock<ILogger<CreatePaymentHandler>>();

        var senderWallet = new Wallet(Guid.NewGuid(), senderTenantId, "USD", now);
        var receiverWallet = new Wallet(Guid.NewGuid(), receiverTenantId, "USD", now);

        tenantContext.SetupGet(context => context.CurrentTenant).Returns(tenant);
        tenantRepository.Setup(repository => repository.GetByIdAsync(receiverTenantId, It.IsAny<CancellationToken>())).ReturnsAsync(receiver);

        idempotencyService
            .Setup(service => service.GetCachedResponseAsync<CreatePaymentResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreatePaymentResult?)null);
        idempotencyService.Setup(service => service.AcquireLockAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        idempotencyService.Setup(service => service.CacheResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CreatePaymentResult>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        idempotencyService.Setup(service => service.ReleaseLockAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        paymentRepository.Setup(repository => repository.GetByIdempotencyKeyAsync(senderTenantId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Payment?)null);
        paymentRepository.Setup(repository => repository.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        paymentRepository.Setup(repository => repository.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        walletRepository.SetupSequence(repository => repository.FindOrCreateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(senderWallet)
            .ReturnsAsync(receiverWallet);

        ledgerRepository.Setup(repository => repository.AddRangeAsync(It.IsAny<IEnumerable<LedgerEntry>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        eventPublisher.Setup(publisher => publisher.PublishAsync("payment.events", It.IsAny<PaymentProcessedEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        transaction.Setup(dbTransaction => dbTransaction.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        transaction.Setup(dbTransaction => dbTransaction.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        unitOfWork.Setup(work => work.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(transaction.Object);
        unitOfWork.Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new CreatePaymentHandler(
            paymentRepository.Object,
            ledgerRepository.Object,
            walletRepository.Object,
            tenantRepository.Object,
            idempotencyService.Object,
            eventPublisher.Object,
            unitOfWork.Object,
            tenantContext.Object,
            metrics.Object,
            logger.Object);

        var result = await handler.Handle(
            new CreatePaymentCommand(
                senderTenantId,
                receiverTenantId,
                $"log-test-{Guid.NewGuid():N}",
                100,
                "USD",
                "Order",
                "{\"orderId\":\"123\"}"),
            CancellationToken.None);

        result.PaymentId.Should().NotBeEmpty();
        logger.Verify(
            log => log.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => HasPaymentSucceededProperties(state, result.PaymentId, senderTenantId)),
                It.Is<Exception?>(exception => exception == null),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static bool HasPaymentSucceededProperties(object state, Guid paymentId, Guid tenantId)
    {
        if (state is not IReadOnlyList<KeyValuePair<string, object?>> properties)
        {
            return false;
        }

        return HasProperty(properties, "{OriginalFormat}", PaymentSucceededTemplate) &&
               HasProperty(properties, "PaymentId", paymentId) &&
               HasProperty(properties, "TenantId", tenantId);
    }

    private static bool HasProperty(
        IReadOnlyList<KeyValuePair<string, object?>> properties,
        string key,
        object expectedValue)
    {
        return properties.Any(property => string.Equals(property.Key, key, StringComparison.Ordinal) && Equals(property.Value, expectedValue));
    }
}
