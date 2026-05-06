using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PayFlow.Application.Common.Exceptions;
using PayFlow.Domain.Interfaces;

namespace PayFlow.Application.Auth.Commands;

public sealed class IssueTokenHandler(ITenantRepository tenantRepository, IConfiguration configuration)
    : IRequestHandler<IssueTokenCommand, IssueTokenResult>
{
    public async Task<IssueTokenResult> Handle(IssueTokenCommand request, CancellationToken cancellationToken)
    {
        var apiKeyHash = HashApiKey(request.ApiKey);
        var tenant = await tenantRepository.GetByApiKeyHashAsync(apiKeyHash, cancellationToken);

        if (tenant is null || !tenant.IsActive)
        {
            throw new UnauthorizedException();
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(15);
        var tenantId = tenant.Id.ToString();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, tenantId),
            new("tenant_id", tenantId),
            new("tenant_name", tenant.Name),
            new("role", request.Role.ToString()),
            new("tier", tenant.Tier.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var jwtKey = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            throw new InvalidOperationException("JWT signing key is required.");
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        var encodedToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new IssueTokenResult(encodedToken, expiresAt, tenant.Id);
    }

    private static string HashApiKey(string apiKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
