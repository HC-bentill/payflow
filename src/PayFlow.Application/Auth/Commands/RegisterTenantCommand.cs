using MediatR;
using PayFlow.Domain.Entities;

namespace PayFlow.Application.Auth.Commands;

public sealed record RegisterTenantCommand(string Name, TenantTier Tier) : IRequest<RegisterTenantResult>;
