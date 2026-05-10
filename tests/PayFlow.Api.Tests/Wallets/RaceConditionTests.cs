using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayFlow.Api.Tests.Auth;
using PayFlow.Api.Tests.Payments;
using PayFlow.Domain.Entities;
using PayFlow.Infrastructure.Persistence;

namespace PayFlow.Api.Tests.Wallets;

public sealed class RaceConditionTests(PayFlowApiFactory factory) : IClassFixture<PayFlowApiFactory>
{
    [Fact]
    public async Task ConcurrentPayments_CannotOverdrawSenderWallet()
    {
        await factory.ResetDatabaseAsync();
        var tenantA = await PaymentTestClient.CreateAuthenticatedClientAsync(factory, $"tenant-a-{Guid.NewGuid():N}");
        var tenantB = await PaymentTestClient.CreateAuthenticatedClientAsync(factory, $"tenant-b-{Guid.NewGuid():N}");
        var walletId = await WalletTestClient.CreateWalletAsync(factory, tenantA.TenantId);
        var topUp = await tenantA.Client.TopUpWalletAsync(walletId, $"topup-{Guid.NewGuid():N}", amount: 100);
        topUp.StatusCode.Should().Be(HttpStatusCode.Created);

        var responses = await Task.WhenAll(
            tenantA.Client.CreatePaymentAsync(tenantB.TenantId, $"payment-{Guid.NewGuid():N}", amount: 100),
            tenantA.Client.CreatePaymentAsync(tenantB.TenantId, $"payment-{Guid.NewGuid():N}", amount: 100));

        responses.Select(response => response.StatusCode)
            .Should()
            .BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.UnprocessableEntity]);

        var tenantAWallets = await tenantA.Client.GetFromJsonAsync<WalletSummaryResponse[]>(
            "/v1/payments/wallets",
            AuthTestClient.JsonOptions);
        tenantAWallets.Should().ContainSingle(wallet => wallet.WalletId == walletId && wallet.Balance == 0);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PayFlowDbContext>();
        var debitCount = await dbContext.LedgerEntries.CountAsync(
            entry => entry.WalletId == walletId && entry.Type == LedgerEntryType.Debit,
            CancellationToken.None);

        debitCount.Should().Be(1);
    }
}
