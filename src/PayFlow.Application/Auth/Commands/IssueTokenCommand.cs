using MediatR;
using PayFlow.Domain.Enums;

namespace PayFlow.Application.Auth.Commands;

public sealed record IssueTokenCommand(string ApiKey, TenantRole Role) : IRequest<IssueTokenResult>;
