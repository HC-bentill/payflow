using Microsoft.AspNetCore.Mvc;
using PayFlow.Domain.Entities;

namespace PayFlow.Api.Controllers;

public static class ControllerTenantExtensions
{
    public static Tenant GetCurrentTenant(this ControllerBase controller)
    {
        return (Tenant)(controller.HttpContext.Items["CurrentTenant"]
            ?? throw new InvalidOperationException("No current tenant is available."));
    }
}
