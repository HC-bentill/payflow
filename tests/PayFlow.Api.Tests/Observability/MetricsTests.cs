using System.Net;
using FluentAssertions;
using PayFlow.Api.Tests.Payments;

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

        var paymentResponse = await authenticated.Client.CreatePaymentAsync(
            receiver.TenantId,
            $"metrics-payment-{Guid.NewGuid():N}");
        paymentResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var metricsResponse = await factory.CreateClient().GetAsync("/metrics", CancellationToken.None);
        var metrics = await metricsResponse.Content.ReadAsStringAsync(CancellationToken.None);

        metricsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        metrics.Should().Contain("payflow_payments_total");
        metrics.Should().Contain("direction=\"debit\"");
        metrics.Should().Contain("direction=\"credit\"");
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

        var metricsResponse = await factory.CreateClient().GetAsync("/metrics", CancellationToken.None);
        var metrics = await metricsResponse.Content.ReadAsStringAsync(CancellationToken.None);

        metricsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        metrics.Should().Contain("payflow_rate_limit_hits_total");
    }
}
