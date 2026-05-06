using Microsoft.AspNetCore.Http;
using PayFlow.Application.Common;
using PayFlow.Domain.Entities;

namespace PayFlow.Infrastructure.Services;

public sealed class TenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public Tenant CurrentTenant
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("No active HTTP context.");

            return (Tenant)(httpContext.Items["CurrentTenant"]
                ?? throw new InvalidOperationException("No current tenant is available."));
        }
    }
}
