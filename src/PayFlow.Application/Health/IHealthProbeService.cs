namespace PayFlow.Application.Health;

public interface IHealthProbeService
{
    Task<DependencyHealthChecks> CheckAsync(CancellationToken ct);
}
