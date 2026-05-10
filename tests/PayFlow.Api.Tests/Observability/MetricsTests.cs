using System.Net;
using FluentAssertions;
using PayFlow.Api.Tests.Payments;
using PayFlow.Api.Tests.Wallets;

namespace PayFlow.Api.Tests.Observability;

public sealed class MetricsTests(PayFlowApiFactory factory) : IClassFixture<PayFlowApiFactory>
{
    [Fact]
    public async Task MetricsEndpoint_ReturnsPrometheusTextFormat()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/metrics", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/plain");
        response.Content.Headers.ContentType?.ToString().Should().Contain("version=0.0.4");
    }

    [Fact]
    public async Task MetricsEndpoint_ContainsPaymentMetricAfterCreatingPayment()
    {
        await factory.ResetDatabaseAsync();
        var authenticated = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        var receiver = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        await WalletTestClient.FundWalletAsync(factory, authenticated, amount: 500);

        var paymentResponse = await authenticated.Client.CreatePaymentAsync(
            receiver.TenantId,
            $"metrics-payment-{Guid.NewGuid():N}");
        paymentResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var metrics = await ReadMetricsUntilContainsAsync(
            "payflow_payments_total",
            "direction=\"debit\"",
            "direction=\"credit\"");
        metrics.Should().Contain("payflow_payments_total");
        metrics.Should().Contain("direction=\"debit\"");
        metrics.Should().Contain("direction=\"credit\"");
    }

    [Fact]
    public async Task MetricsEndpoint_ContainsTopUpAndInsufficientFundsMetrics()
    {
        await factory.ResetDatabaseAsync();
        var authenticated = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        var receiver = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        var walletId = await WalletTestClient.CreateWalletAsync(factory, authenticated.TenantId);

        var topUpResponse = await authenticated.Client.TopUpWalletAsync(
            walletId,
            $"metrics-topup-{Guid.NewGuid():N}",
            amount: 50);
        topUpResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var paymentResponse = await authenticated.Client.CreatePaymentAsync(
            receiver.TenantId,
            $"metrics-insufficient-{Guid.NewGuid():N}",
            amount: 100);
        paymentResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var metrics = await ReadMetricsUntilContainsAsync(
            "payflow_topups_total",
            "payflow_insufficient_funds_total");
        metrics.Should().Contain("payflow_topups_total");
        metrics.Should().Contain("payflow_insufficient_funds_total");
    }

    [Fact]
    public async Task MetricsEndpoint_ContainsRateLimitMetricAfterRateLimitHit()
    {
        await factory.ResetDatabaseAsync();
        var authenticated = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);

        HttpResponseMessage? rejected = null;
        for (var i = 0; i < 101; i++)
        {
            rejected = await authenticated.Client.GetAsync("/v1/payments", CancellationToken.None);
        }

        rejected!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        var metrics = await ReadMetricsUntilContainsAsync("payflow_rate_limit_hits_total");
        metrics.Should().Contain("payflow_rate_limit_hits_total");
    }

    private async Task<string> ReadMetricsUntilContainsAsync(params string[] expected)
    {
        using var client = factory.CreateClient();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        var metrics = string.Empty;

        while (DateTime.UtcNow < deadline)
        {
            var response = await client.GetAsync("/metrics", CancellationToken.None);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            metrics = await response.Content.ReadAsStringAsync(CancellationToken.None);

            if (expected.All(item => metrics.Contains(item, StringComparison.Ordinal)))
            {
                return metrics;
            }

            await Task.Delay(100, CancellationToken.None);
        }

        return metrics;
    }
}
