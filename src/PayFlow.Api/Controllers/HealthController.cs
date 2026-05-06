using MediatR;
using Microsoft.AspNetCore.Mvc;
using PayFlow.Application.Health;

namespace PayFlow.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var health = await mediator.Send(new GetHealthStatusQuery(), ct);
        var statusCode = health.IsHealthy
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;

        return StatusCode(statusCode, health);
    }
}
