using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PayFlow.Api.Controllers.Models;
using PayFlow.Application.Common;
using PayFlow.Application.Wallets.Commands;
using PayFlow.Application.Wallets.Queries;

namespace PayFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("v1/wallets")]
public sealed class WalletsController(IMediator mediator, ITenantContext tenantContext) : ControllerBase
{
    [HttpPost("{walletId:guid}/topup")]
    public async Task<IActionResult> TopUp(Guid walletId, TopUpWalletRequest request, CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKeyValues) ||
            string.IsNullOrWhiteSpace(idempotencyKeyValues.ToString()))
        {
            return BadRequest(new { error = "Idempotency-Key header is required" });
        }

        var result = await mediator.Send(
            new TopUpWalletCommand(
                tenantContext.CurrentTenant.Id,
                walletId,
                idempotencyKeyValues.ToString(),
                request.Amount,
                request.Currency),
            ct);

        if (result.IsReplay)
        {
            Response.Headers["Idempotent-Replayed"] = "true";
        }

        TopUpWalletResponse response = result;

        return Created($"/v1/wallets/{walletId}/topups", response);
    }

    [HttpGet("{walletId:guid}/topups")]
    public async Task<IActionResult> TopUps(
        Guid walletId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetTopUpHistoryQuery(
                tenantContext.CurrentTenant.Id,
                walletId,
                page,
                Math.Clamp(pageSize, 1, 100)),
            ct);

        return Ok(result);
    }
}
