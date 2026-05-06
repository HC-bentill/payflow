using PayFlow.Domain.Entities;

namespace PayFlow.Domain.Events;

public sealed record TenantRegisteredEvent(Guid TenantId, string Name, TenantTier Tier, DateTime OccurredAt);
