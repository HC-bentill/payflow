using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PayFlow.Api.Controllers.Models;
using PayFlow.Application.Common;
using PayFlow.Application.Webhooks.Commands;
using PayFlow.Application.Webhooks.Queries;

namespace PayFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("v1/webhooks")]
public sealed class WebhooksController(IMediator mediator, ITenantContext tenantContext) : ControllerBase
{
    [HttpPost("endpoints")]
    public async Task<IActionResult> RegisterEndpoint(RegisterWebhookEndpointRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new RegisterWebhookEndpointCommand(
                tenantContext.CurrentTenant.Id,
                request.Url,
                request.Secret),
            ct);

        RegisterWebhookEndpointResponse response = result;

        return CreatedAtAction(nameof(ListEndpoints), new { endpointId = result.WebhookEndpointId }, response);
    }

    [HttpGet("endpoints")]
    public async Task<IActionResult> ListEndpoints(CancellationToken ct)
    {
        var endpoints = await mediator.Send(
            new ListWebhookEndpointsQuery(tenantContext.CurrentTenant.Id),
            ct);

        return Ok(endpoints);
    }

    [HttpGet("deliveries")]
    public async Task<IActionResult> ListDeliveries(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool failedOnly = false,
        CancellationToken ct = default)
    {
        var deliveries = await mediator.Send(
            new ListWebhookDeliveryLogsQuery(
                tenantContext.CurrentTenant.Id,
                page,
                Math.Clamp(pageSize, 1, 100),
                failedOnly),
            ct);

        return Ok(deliveries);
    }

    [HttpPost("deliveries/{deliveryLogId:guid}/replay")]
    public async Task<IActionResult> ReplayDelivery(Guid deliveryLogId, CancellationToken ct)
    {
        var result = await mediator.Send(
            new ReplayWebhookCommand(deliveryLogId, tenantContext.CurrentTenant.Id),
            ct);

        return Ok(result);
    }
}
