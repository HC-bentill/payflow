using System.Security.Cryptography;
using System.Text;
using MediatR;
using PayFlow.Application.Common.Exceptions;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Interfaces;

namespace PayFlow.Application.Auth.Commands;

public sealed class RegisterTenantHandler(ITenantRepository tenantRepository)
    : IRequestHandler<RegisterTenantCommand, RegisterTenantResult>
{
    public async Task<RegisterTenantResult> Handle(RegisterTenantCommand request, CancellationToken cancellationToken)
    {
        if (await tenantRepository.ExistsAsync(request.Name, cancellationToken))
        {
            throw new TenantAlreadyExistsException();
        }

        var rawKey = GenerateApiKey();
        var apiKeyHash = HashApiKey(rawKey);
        var tenant = new Tenant(
            Guid.NewGuid(),
            request.Name,
            apiKeyHash,
            request.Tier,
            DateTime.UtcNow,
            true);

        await tenantRepository.AddAsync(tenant, cancellationToken);

        return new RegisterTenantResult(tenant.Id, rawKey);
    }

    private static string GenerateApiKey()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return $"pf_live_{key}";
    }

    private static string HashApiKey(string apiKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
