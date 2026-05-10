using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PayFlow.Api.Tests.Auth;
using PayFlow.Api.Tests.Payments;

namespace PayFlow.Api.Tests.Wallets;

public sealed class InsufficientFundsTests(PayFlowApiFactory factory) : IClassFixture<PayFlowApiFactory>
{
    [Fact]
    public async Task Payments_RequireSufficientSenderBalance()
    {
        await factory.ResetDatabaseAsync();
        var tenantA = await PaymentTestClient.CreateAuthenticatedClientAsync(factory, $"tenant-a-{Guid.NewGuid():N}");
        var tenantB = await PaymentTestClient.CreateAuthenticatedClientAsync(factory, $"tenant-b-{Guid.NewGuid():N}");

        var unfundedPayment = await tenantA.Client.CreatePaymentAsync(
            tenantB.TenantId,
            $"payment-{Guid.NewGuid():N}",
            amount: 100);

        unfundedPayment.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var insufficientFunds = await unfundedPayment.Content.ReadFromJsonAsync<InsufficientFundsResponse>(
            AuthTestClient.JsonOptions,
            CancellationToken.None);
        insufficientFunds.Should().NotBeNull();
        insufficientFunds!.Error.Should().Be("insufficient_funds");
        insufficientFunds.RequiredAmount.Should().Be(100);
        insufficientFunds.AvailableBalance.Should().Be(0);
        insufficientFunds.Currency.Should().Be("USD");
        insufficientFunds.WalletId.Should().NotBeEmpty();

        var topUp = await tenantA.Client.TopUpWalletAsync(
            insufficientFunds.WalletId,
            $"topup-{Guid.NewGuid():N}",
            amount: 200);
        topUp.StatusCode.Should().Be(HttpStatusCode.Created);

        var successfulPayment = await tenantA.Client.CreatePaymentAsync(
            tenantB.TenantId,
            $"payment-{Guid.NewGuid():N}",
            amount: 100);
        successfulPayment.StatusCode.Should().Be(HttpStatusCode.Created);

        var overdraftPayment = await tenantA.Client.CreatePaymentAsync(
            tenantB.TenantId,
            $"payment-{Guid.NewGuid():N}",
            amount: 200);
        overdraftPayment.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var tenantAWallets = await tenantA.Client.GetFromJsonAsync<WalletSummaryResponse[]>(
            "/v1/payments/wallets",
            AuthTestClient.JsonOptions);
        var tenantBWallets = await tenantB.Client.GetFromJsonAsync<WalletSummaryResponse[]>(
            "/v1/payments/wallets",
            AuthTestClient.JsonOptions);

        tenantAWallets.Should().ContainSingle(wallet => wallet.WalletId == insufficientFunds.WalletId && wallet.Balance == 100);
        tenantBWallets.Should().ContainSingle(wallet => wallet.Balance == 100);
    }
}
