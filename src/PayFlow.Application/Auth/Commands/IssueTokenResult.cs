namespace PayFlow.Application.Auth.Commands;

public sealed record IssueTokenResult(string Token, DateTime ExpiresAt, Guid TenantId);
