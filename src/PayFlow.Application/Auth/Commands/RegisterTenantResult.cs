namespace PayFlow.Application.Auth.Commands;

public sealed record RegisterTenantResult(Guid TenantId, string ApiKey);
