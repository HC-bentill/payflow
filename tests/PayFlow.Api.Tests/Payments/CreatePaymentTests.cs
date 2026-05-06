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
        var authenticated = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);

        var response = await authenticated.Client.PostAsJsonAsync(
            "/v1/payments",
            new CreatePaymentRequest(100, "USD", "Order #1234", "{\"orderId\":\"1234\"}"),
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithValidPayload_ReturnsCreatedWithSucceededPayment()
    {
        await factory.ResetDatabaseAsync();
        var authenticated = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);

        var response = await authenticated.Client.CreatePaymentAsync($"idem-{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>(
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        payment.Should().NotBeNull();
        payment!.PaymentId.Should().NotBeEmpty();
        payment.Status.Should().Be("Succeeded");
    }

    [Fact]
    public async Task Create_WithSameIdempotencyKeyTwice_ReplaysSecondResponse()
    {
        await factory.ResetDatabaseAsync();
        var authenticated = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        var idempotencyKey = $"idem-{Guid.NewGuid():N}";

        var first = await authenticated.Client.CreatePaymentAsync(idempotencyKey);
        var second = await authenticated.Client.CreatePaymentAsync(idempotencyKey);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        second.Headers.TryGetValues("Idempotent-Replayed", out var values).Should().BeTrue();
        values.Should().Contain("true");

        var firstPayment = await first.Content.ReadFromJsonAsync<PaymentResponse>(
            AuthTestClient.JsonOptions,
            CancellationToken.None);
        var secondPayment = await second.Content.ReadFromJsonAsync<PaymentResponse>(
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        secondPayment!.PaymentId.Should().Be(firstPayment!.PaymentId);
    }

    [Fact]
    public async Task Create_WithZeroAmount_ReturnsBadRequest()
    {
        await factory.ResetDatabaseAsync();
        var authenticated = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);

        var response = await authenticated.Client.CreatePaymentAsync($"idem-{Guid.NewGuid():N}", amount: 0);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithAmountAboveLimit_ReturnsBadRequest()
    {
        await factory.ResetDatabaseAsync();
        var authenticated = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);

        var response = await authenticated.Client.CreatePaymentAsync(
            $"idem-{Guid.NewGuid():N}",
            amount: 1000000m);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithTwoLetterCurrency_ReturnsBadRequest()
    {
        await factory.ResetDatabaseAsync();
        var authenticated = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);

        var response = await authenticated.Client.CreatePaymentAsync(
            $"idem-{Guid.NewGuid():N}",
            currency: "US");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithLowercaseCurrency_ReturnsBadRequest()
    {
        await factory.ResetDatabaseAsync();
        var authenticated = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);

        var response = await authenticated.Client.CreatePaymentAsync(
            $"idem-{Guid.NewGuid():N}",
            currency: "usd");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithGhsCurrency_ReturnsCreated()
    {
        await factory.ResetDatabaseAsync();
        var authenticated = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);

        var response = await authenticated.Client.CreatePaymentAsync(
            $"idem-{Guid.NewGuid():N}",
            currency: "GHS");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
