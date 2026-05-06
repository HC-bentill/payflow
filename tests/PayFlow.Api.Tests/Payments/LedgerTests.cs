using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PayFlow.Api.Tests.Auth;

namespace PayFlow.Api.Tests.Payments;

public sealed class LedgerTests(PayFlowApiFactory factory) : IClassFixture<PayFlowApiFactory>
{
    [Fact]
    public async Task GetPayment_AfterSuccessfulPayment_ReturnsTwoLedgerEntries()
    {
        await factory.ResetDatabaseAsync();
        var authenticated = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        var createResponse = await authenticated.Client.CreatePaymentAsync(
            $"idem-{Guid.NewGuid():N}",
            amount: 100);
        var created = await createResponse.Content.ReadFromJsonAsync<PaymentResponse>(
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        var response = await authenticated.Client.GetAsync(
            $"/v1/payments/{created!.PaymentId}",
            CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payment = await response.Content.ReadFromJsonAsync<PaymentDetailsResponse>(
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        payment.Should().NotBeNull();
        payment!.LedgerEntries.Should().HaveCount(2);
        payment.LedgerEntries.Select(entry => entry.Type).Should().BeEquivalentTo(["Debit", "Credit"]);
        payment.LedgerEntries.Should().OnlyContain(entry => entry.Amount == 100);
    }

    [Fact]
    public async Task WalletBalance_AfterSimpleUsdPayment_ReturnsZero()
    {
        await factory.ResetDatabaseAsync();
        var authenticated = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        await authenticated.Client.CreatePaymentAsync($"idem-{Guid.NewGuid():N}", amount: 100);

        var response = await authenticated.Client.GetAsync(
            "/v1/payments/wallet/USD",
            CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var wallet = await response.Content.ReadFromJsonAsync<WalletBalanceResponse>(
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        // Debit and Credit entries cancel out for this simple transfer. In a real system,
        // Credits represent merchant receipts and Debits represent payouts, so balance = credits - debits.
        wallet.Should().NotBeNull();
        wallet!.Balance.Should().Be(0);
    }
}
