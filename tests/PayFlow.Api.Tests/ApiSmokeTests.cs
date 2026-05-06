using FluentAssertions;

namespace PayFlow.Api.Tests;

public sealed class ApiSmokeTests(PayFlowApiFactory factory)
    : IClassFixture<PayFlowApiFactory>
{
    [Fact]
    public async Task SwaggerUi_Loads()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/index.html", CancellationToken.None);

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(CancellationToken.None);
        content.Should().Contain("Swagger UI");
    }

    [Fact]
    public async Task MetricsEndpoint_ReturnsPrometheusText()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/metrics", CancellationToken.None);

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/plain");
    }
}
