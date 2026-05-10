using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PayFlow.Api.Tests.Auth;
using PayFlow.Api.Tests.Payments;
using PayFlow.Domain.Entities;

namespace PayFlow.Api.Tests.Wallets;

internal static class WalletTestClient
{
    public static async Task<Guid> CreateWalletAsync(
        PayFlowApiFactory factory,
        Guid tenantId,
        string currency = "USD",
        CancellationToken ct = default)
    {
        var walletId = Guid.Empty;

        await factory.SeedAsync(async (dbContext, seedCt) =>
        {
            var wallet = await dbContext.Wallets.FirstOrDefaultAsync(
                item => item.OwnerId == tenantId && item.Currency == currency,
                seedCt);

            if (wallet is null)
            {
                wallet = new Wallet(Guid.NewGuid(), tenantId, currency, DateTime.UtcNow);
                await dbContext.Wallets.AddAsync(wallet, seedCt);
                await dbContext.SaveChangesAsync(seedCt);
            }

            walletId = wallet.Id;
        }, ct);

        return walletId;
    }

    public static async Task<Guid> FundWalletAsync(
        PayFlowApiFactory factory,
        AuthenticatedClient tenant,
        decimal amount = 500,
        string currency = "USD",
        CancellationToken ct = default)
    {
        var walletId = await CreateWalletAsync(factory, tenant.TenantId, currency, ct);
        var response = await tenant.Client.TopUpWalletAsync(
            walletId,
            $"topup-{Guid.NewGuid():N}",
            amount,
            currency,
            ct);
        response.EnsureSuccessStatusCode();

        return walletId;
    }

    public static async Task<HttpResponseMessage> TopUpWalletAsync(
        this HttpClient client,
        Guid walletId,
        string idempotencyKey,
        decimal amount = 100,
        string currency = "USD",
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/wallets/{walletId}/topup")
        {
            Content = JsonContent.Create(
                new TopUpRequest(amount, currency),
                options: AuthTestClient.JsonOptions)
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        return await client.SendAsync(request, ct);
    }
}

internal sealed record TopUpRequest(decimal Amount, string Currency);

internal sealed record TopUpResponse(
    Guid TopUpId,
    Guid WalletId,
    decimal Amount,
    string Currency,
    decimal NewBalance,
    DateTime CreatedAt);

internal sealed record TopUpHistoryResponse(
    TopUpHistoryItemResponse[] Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

internal sealed record TopUpHistoryItemResponse(
    Guid TopUpId,
    Guid WalletId,
    decimal Amount,
    string Currency,
    string Status,
    DateTime CreatedAt);

internal sealed record InsufficientFundsResponse(
    string Error,
    string Message,
    decimal RequiredAmount,
    decimal AvailableBalance,
    string Currency,
    Guid WalletId);
