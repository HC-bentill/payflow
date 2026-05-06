namespace PayFlow.Application.Health;

public sealed record DependencyHealthChecks(bool Postgres, bool Redis);
