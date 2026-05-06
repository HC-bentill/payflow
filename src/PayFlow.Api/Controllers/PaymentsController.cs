using MediatR;
using Microsoft.AspNetCore.Mvc;
using PayFlow.Application.Payments.Queries;

namespace PayFlow.Api.Controllers;

[ApiController]
[Route("v1/payments")]
public sealed class PaymentsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var payments = await mediator.Send(new ListPaymentsQuery(), ct);

        return Ok(payments);
    }
}
