using MediatR;

namespace PayFlow.Application.Health;

public sealed class GetHealthStatusQueryHandler(
    IHealthProbeService healthProbeService,
    IApplicationEnvironment applicationEnvironment)
    : IRequestHandler<GetHealthStatusQuery, HealthStatusResponse>
{
    private const string Version = "1.0.0";

    public async Task<HealthStatusResponse> Handle(GetHealthStatusQuery request, CancellationToken cancellationToken)
    {
        var checks = await healthProbeService.CheckAsync(cancellationToken);
        var status = checks.Postgres && checks.Redis ? "healthy" : "unhealthy";

        return new HealthStatusResponse(status, checks, Version, applicationEnvironment.EnvironmentName);
    }
}
