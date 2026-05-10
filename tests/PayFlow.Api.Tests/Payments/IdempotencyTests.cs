using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayFlow.Api.Tests.Wallets;
using PayFlow.Infrastructure.Persistence;

namespace PayFlow.Api.Tests.Payments;

public sealed class IdempotencyTests(PayFlowApiFactory factory) : IClassFixture<PayFlowApiFactory>
{
    [Fact]
    public async Task ConcurrentRequests_WithSameIdempotencyKey_CreateOnlyOnePayment()
    {
        await factory.ResetDatabaseAsync();
        var sender = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        var receiver = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        var idempotencyKey = $"idem-{Guid.NewGuid():N}";
        await WalletTestClient.FundWalletAsync(factory, sender, amount: 500);

        var responses = await Task.WhenAll(
            sender.Client.CreatePaymentAsync(receiver.TenantId, idempotencyKey),
            sender.Client.CreatePaymentAsync(receiver.TenantId, idempotencyKey));

        responses.Select(response => response.StatusCode)
            .Should()
            .OnlyContain(status => status == HttpStatusCode.Created || status == HttpStatusCode.Conflict);
        responses.Select(response => response.StatusCode).Should().Contain(HttpStatusCode.Created);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PayFlowDbContext>();
        var paymentCount = await dbContext.Payments.CountAsync(
            payment => payment.TenantId == sender.TenantId && payment.IdempotencyKey == idempotencyKey,
            CancellationToken.None);

        paymentCount.Should().Be(1);
    }
}
