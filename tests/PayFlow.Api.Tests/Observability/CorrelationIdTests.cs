using FluentAssertions;

namespace PayFlow.Api.Tests.Observability;

public sealed class CorrelationIdTests(PayFlowApiFactory factory) : IClassFixture<PayFlowApiFactory>
{
    private const string HeaderName = "X-Correlation-ID";

    [Fact]
    public async Task RequestWithoutCorrelationId_ReturnsGeneratedCorrelationId()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/metrics", CancellationToken.None);

        response.Headers.TryGetValues(HeaderName, out var values).Should().BeTrue();
        values!.Single().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RequestWithCorrelationId_EchoesCorrelationId()
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/metrics");
        request.Headers.Add(HeaderName, "my-trace-123");

        var response = await client.SendAsync(request, CancellationToken.None);

        response.Headers.TryGetValues(HeaderName, out var values).Should().BeTrue();
        values!.Single().Should().Be("my-trace-123");
    }

    [Fact]
    public async Task RequestsWithoutCorrelationId_GetDifferentGeneratedIds()
    {
        var client = factory.CreateClient();

        var first = await client.GetAsync("/metrics", CancellationToken.None);
        var second = await client.GetAsync("/metrics", CancellationToken.None);

        var firstCorrelationId = GetHeader(first);
        var secondCorrelationId = GetHeader(second);

        firstCorrelationId.Should().NotBe(secondCorrelationId);
    }

    private static string GetHeader(HttpResponseMessage response)
    {
        response.Headers.TryGetValues(HeaderName, out var values).Should().BeTrue();

        return values!.Single();
    }
}
