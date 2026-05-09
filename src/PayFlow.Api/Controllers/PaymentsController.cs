using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PayFlow.Api.Controllers.Models;
using PayFlow.Application.Common;
using PayFlow.Application.Payments.Commands;
using PayFlow.Application.Payments.Queries;

namespace PayFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("v1/payments")]
public sealed class PaymentsController(IMediator mediator, ITenantContext tenantContext) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreatePaymentRequest request, CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKeyValues) ||
            string.IsNullOrWhiteSpace(idempotencyKeyValues.ToString()))
        {
            return BadRequest(new { error = "Idempotency-Key header is required" });
        }

        var result = await mediator.Send(
            new CreatePaymentCommand(
                tenantContext.CurrentTenant.Id,
                request.ReceiverTenantId,
                idempotencyKeyValues.ToString(),
                request.Amount,
                request.Currency,
                request.Description,
                request.Metadata),
            ct);

        if (result.IsReplay)
        {
            Response.Headers["Idempotent-Replayed"] = "true";
        }

        PaymentResponse response = result;

        return CreatedAtAction(nameof(Get), new { id = result.PaymentId }, response);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var payment = await mediator.Send(
            new GetPaymentQuery(id, tenantContext.CurrentTenant.Id),
            ct);

        return Ok(payment);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var payments = await mediator.Send(
            new ListPaymentsQuery(tenantContext.CurrentTenant.Id, page, Math.Clamp(pageSize, 1, 100)),
            ct);

        return Ok(payments);
    }

    [HttpGet("wallets")]
    public async Task<IActionResult> Wallets(CancellationToken ct)
    {
        var wallets = await mediator.Send(new ListWalletsQuery(tenantContext.CurrentTenant.Id), ct);
        return Ok(wallets);
    }

    [HttpGet("wallets/{walletId:guid}/balance")]
    public async Task<IActionResult> WalletBalance(Guid walletId, CancellationToken ct)
    {
        var wallets = await mediator.Send(new ListWalletsQuery(tenantContext.CurrentTenant.Id), ct);
        if (!wallets.Any(wallet => wallet.WalletId == walletId))
        {
            return Forbid();
        }

        var balance = await mediator.Send(
            new GetWalletBalanceQuery(tenantContext.CurrentTenant.Id, walletId),
            ct);

        return Ok(balance);
    }
}
