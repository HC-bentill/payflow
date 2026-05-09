using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PayFlow.Api.Tests.Auth;

namespace PayFlow.Api.Tests.Payments;

public sealed class CreatePaymentTests(PayFlowApiFactory factory) : IClassFixture<PayFlowApiFactory>
{
    [Fact]
    public async Task Create_WithoutIdempotencyKey_ReturnsBadRequest()
    {
        await factory.ResetDatabaseAsync();
        var sender = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        var receiver = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);

        var response = await sender.Client.PostAsJsonAsync(
            "/v1/payments",
            new CreatePaymentRequest(100, "USD", receiver.TenantId, "Order #1234", "{\"orderId\":\"1234\"}"),
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithValidPayload_ReturnsCreatedWithSucceededPayment()
    {
        await factory.ResetDatabaseAsync();
        var sender = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        var receiver = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);

        var response = await sender.Client.CreatePaymentAsync(receiver.TenantId, $"idem-{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>(AuthTestClient.JsonOptions, CancellationToken.None);

        payment.Should().NotBeNull();
        payment!.PaymentId.Should().NotBeEmpty();
        payment.SenderTenantId.Should().Be(sender.TenantId);
        payment.ReceiverTenantId.Should().Be(receiver.TenantId);
        payment.Status.Should().Be("Succeeded");
    }

    [Fact]
    public async Task Create_WithSameSenderAndReceiver_ReturnsBadRequest()
    {
        await factory.ResetDatabaseAsync();
        var sender = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);

        var response = await sender.Client.CreatePaymentAsync(sender.TenantId, $"idem-{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithUnknownReceiver_ReturnsNotFound()
    {
        await factory.ResetDatabaseAsync();
        var sender = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);

        var response = await sender.Client.CreatePaymentAsync(Guid.NewGuid(), $"idem-{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_WithSameIdempotencyKeyTwice_ReplaysSecondResponse()
    {
        await factory.ResetDatabaseAsync();
        var sender = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        var receiver = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        var idempotencyKey = $"idem-{Guid.NewGuid():N}";

        var first = await sender.Client.CreatePaymentAsync(receiver.TenantId, idempotencyKey);
        var second = await sender.Client.CreatePaymentAsync(receiver.TenantId, idempotencyKey);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        second.Headers.TryGetValues("Idempotent-Replayed", out var values).Should().BeTrue();
        values.Should().Contain("true");

        var firstPayment = await first.Content.ReadFromJsonAsync<PaymentResponse>(AuthTestClient.JsonOptions, CancellationToken.None);
        var secondPayment = await second.Content.ReadFromJsonAsync<PaymentResponse>(AuthTestClient.JsonOptions, CancellationToken.None);

        secondPayment!.PaymentId.Should().Be(firstPayment!.PaymentId);
    }

    [Fact]
    public async Task Create_UpdatesSenderAndReceiverWalletBalances()
    {
        await factory.ResetDatabaseAsync();
        var sender = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        var receiver = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);

        var createResponse = await sender.Client.CreatePaymentAsync(receiver.TenantId, $"idem-{Guid.NewGuid():N}", amount: 100);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var senderWalletsResponse = await sender.Client.GetFromJsonAsync<WalletSummaryResponse[]>("/v1/payments/wallets", AuthTestClient.JsonOptions);
        var receiverWalletsResponse = await receiver.Client.GetFromJsonAsync<WalletSummaryResponse[]>("/v1/payments/wallets", AuthTestClient.JsonOptions);

        senderWalletsResponse.Should().NotBeNullOrEmpty();
        receiverWalletsResponse.Should().NotBeNullOrEmpty();
        senderWalletsResponse![0].Balance.Should().Be(-100);
        receiverWalletsResponse![0].Balance.Should().Be(100);
    }
}
