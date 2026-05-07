using PayFlow.Domain.Interfaces;

namespace PayFlow.Api.Middleware;

public sealed class TenantContextMiddleware(
    RequestDelegate next,
    ILogger<TenantContextMiddleware> logger)
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
            logger.LogWarning(
                "Authenticated request on path {Path} had missing or invalid tenant claim",
                context.Request.Path.Value);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var tenantRepository = context.RequestServices.GetRequiredService<ITenantRepository>();
        var tenant = await tenantRepository.GetByIdAsync(tenantId, context.RequestAborted);

        if (tenant is null || !tenant.IsActive)
        {
            logger.LogWarning(
                "Tenant {TenantId} was not found or inactive for path {Path}",
                tenantId,
                context.Request.Path.Value);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        context.Items["CurrentTenant"] = tenant;
        logger.LogDebug(
            "Tenant {TenantId} resolved for path {Path}",
            tenant.Id,
            context.Request.Path.Value);

        await next(context);
    }
}
