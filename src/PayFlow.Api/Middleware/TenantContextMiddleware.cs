using PayFlow.Domain.Interfaces;

namespace PayFlow.Api.Middleware;

public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var tenantIdClaim = context.User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var tenantRepository = context.RequestServices.GetRequiredService<ITenantRepository>();
        var tenant = await tenantRepository.GetByIdAsync(tenantId, context.RequestAborted);

        if (tenant is null || !tenant.IsActive)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        context.Items["CurrentTenant"] = tenant;

        await next(context);
    }
}
