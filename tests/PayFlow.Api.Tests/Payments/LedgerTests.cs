using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PayFlow.Api.Tests.Auth;

namespace PayFlow.Api.Tests.Payments;

public sealed class LedgerTests(PayFlowApiFactory factory) : IClassFixture<PayFlowApiFactory>
{
    [Fact]
    public async Task GetPayment_AfterSuccessfulPayment_ReturnsTwoLedgerEntriesAndWalletIds()
    {
        await factory.ResetDatabaseAsync();
        var sender = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        var receiver = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);

        var createResponse = await sender.Client.CreatePaymentAsync(receiver.TenantId, $"idem-{Guid.NewGuid():N}", amount: 100);
        var created = await createResponse.Content.ReadFromJsonAsync<PaymentResponse>(AuthTestClient.JsonOptions, CancellationToken.None);

        var response = await sender.Client.GetAsync($"/v1/payments/{created!.PaymentId}", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payment = await response.Content.ReadFromJsonAsync<PaymentDetailsResponse>(AuthTestClient.JsonOptions, CancellationToken.None);

        payment.Should().NotBeNull();
        payment!.LedgerEntries.Should().HaveCount(2);
        payment.LedgerEntries.Select(entry => entry.Type).Should().BeEquivalentTo(["Debit", "Credit"]);
        payment.SenderWalletId.Should().NotBeEmpty();
        payment.ReceiverWalletId.Should().NotBeEmpty();
        payment.SenderWalletId.Should().NotBe(payment.ReceiverWalletId);
        payment.LedgerEntries.Single(entry => entry.Type == "Debit").WalletId.Should().Be(payment.SenderWalletId);
        payment.LedgerEntries.Single(entry => entry.Type == "Credit").WalletId.Should().Be(payment.ReceiverWalletId);
    }

    [Fact]
    public async Task WalletBalances_AfterSimpleUsdPayment_AreNegativeForSenderAndPositiveForReceiver()
    {
        await factory.ResetDatabaseAsync();
        var sender = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        var receiver = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);

        await sender.Client.CreatePaymentAsync(receiver.TenantId, $"idem-{Guid.NewGuid():N}", amount: 100);

        var senderWallets = await sender.Client.GetFromJsonAsync<WalletSummaryResponse[]>("/v1/payments/wallets", AuthTestClient.JsonOptions);
        var receiverWallets = await receiver.Client.GetFromJsonAsync<WalletSummaryResponse[]>("/v1/payments/wallets", AuthTestClient.JsonOptions);

        senderWallets.Should().NotBeNullOrEmpty();
        receiverWallets.Should().NotBeNullOrEmpty();

        senderWallets![0].Balance.Should().BeNegative();
        receiverWallets![0].Balance.Should().BePositive();
    }

    [Fact]
    public async Task WalletsEndpoint_ReturnsOwnedWalletsForEachTenant()
    {
        await factory.ResetDatabaseAsync();
        var sender = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        var receiver = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);

        await sender.Client.CreatePaymentAsync(receiver.TenantId, $"idem-{Guid.NewGuid():N}", amount: 100);

        var senderWalletsResponse = await sender.Client.GetAsync("/v1/payments/wallets", CancellationToken.None);
        var receiverWalletsResponse = await receiver.Client.GetAsync("/v1/payments/wallets", CancellationToken.None);

        senderWalletsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        receiverWalletsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var senderWallets = await senderWalletsResponse.Content.ReadFromJsonAsync<WalletSummaryResponse[]>(AuthTestClient.JsonOptions, CancellationToken.None);
        var receiverWallets = await receiverWalletsResponse.Content.ReadFromJsonAsync<WalletSummaryResponse[]>(AuthTestClient.JsonOptions, CancellationToken.None);

        senderWallets.Should().ContainSingle();
        receiverWallets.Should().ContainSingle();
        senderWallets![0].WalletId.Should().NotBe(receiverWallets![0].WalletId);
    }
}
