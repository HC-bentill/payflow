using System.Text.Json.Serialization;

namespace PayFlow.Application.Health;

public sealed record HealthStatusResponse(
    string Status,
    DependencyHealthChecks Checks,
    string Version,
    string Environment)
{
    [JsonIgnore]
    public bool IsHealthy => Checks.Postgres && Checks.Redis;
}
