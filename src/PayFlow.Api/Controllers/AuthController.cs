using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PayFlow.Application.Auth.Commands;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;

namespace PayFlow.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("v1/auth")]
public sealed class AuthController(IMediator mediator) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterTenantRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new RegisterTenantCommand(request.Name, request.Tier), ct);

        return Created("/v1/auth/register", new
        {
            result.TenantId,
            result.ApiKey,
            Message = "Store this API key securely - it will not be shown again."
        });
    }

    [HttpPost("token")]
    public async Task<IActionResult> Token(IssueTokenRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new IssueTokenCommand(request.ApiKey, request.Role), ct);

        return Ok(new
        {
            result.Token,
            result.ExpiresAt,
            result.TenantId
        });
    }
}

public sealed record RegisterTenantRequest(string Name, TenantTier Tier);

public sealed record IssueTokenRequest(string ApiKey, TenantRole Role);
