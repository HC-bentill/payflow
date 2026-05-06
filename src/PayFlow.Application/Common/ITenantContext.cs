using PayFlow.Domain.Entities;

namespace PayFlow.Application.Common;

public interface ITenantContext
{
    Tenant CurrentTenant { get; }
}
