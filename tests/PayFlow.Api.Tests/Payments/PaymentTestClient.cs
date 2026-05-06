using System.Net.Http.Json;
using PayFlow.Api.Tests.Auth;
using PayFlow.Domain.Enums;

namespace PayFlow.Api.Tests.Payments;

internal static class PaymentTestClient
{
    public static async Task<AuthenticatedClient> CreateAuthenticatedClientAsync(
        PayFlowApiFactory factory,
        string? tenantName = null,
        CancellationToken ct = default)
    {
        var client = factory.CreateClient();
        var registration = await client.RegisterTenantAsync(
            tenantName ?? $"tenant-{Guid.NewGuid():N}",
            ct: ct);
        var token = await client.IssueTokenAsync(registration.ApiKey, TenantRole.Developer, ct);
        client.UseBearerToken(token.Token);

        return new AuthenticatedClient(client, registration.TenantId);
    }

    public static async Task<HttpResponseMessage> CreatePaymentAsync(
        this HttpClient client,
        string idempotencyKey,
        decimal amount = 100,
        string currency = "USD",
        string? description = "Order #1234",
        string? metadata = "{\"orderId\":\"1234\"}",
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/payments")
        {
            Content = JsonContent.Create(
                new CreatePaymentRequest(amount, currency, description, metadata),
                options: AuthTestClient.JsonOptions)
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        return await client.SendAsync(request, ct);
    }
}

internal sealed record AuthenticatedClient(HttpClient Client, Guid TenantId);

internal sealed record CreatePaymentRequest(
    decimal Amount,
    string Currency,
    string? Description,
    string? Metadata);

internal sealed record PaymentResponse(
    Guid PaymentId,
    string IdempotencyKey,
    decimal Amount,
    string Currency,
    string Status,
    string? Description,
    DateTime CreatedAt);

internal sealed record PaymentDetailsResponse(
    Guid PaymentId,
    string IdempotencyKey,
    decimal Amount,
    string Currency,
    string Status,
    string? Description,
    string? Metadata,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    LedgerEntryResponse[] LedgerEntries);

internal sealed record LedgerEntryResponse(
    Guid Id,
    string Type,
    decimal Amount,
    string Currency,
    DateTime CreatedAt);

internal sealed record ListPaymentsResponse(
    PaymentSummaryResponse[] Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

internal sealed record PaymentSummaryResponse(
    Guid PaymentId,
    decimal Amount,
    string Currency,
    string Status,
    DateTime CreatedAt);

internal sealed record WalletBalanceResponse(
    Guid TenantId,
    string Currency,
    decimal Balance,
    DateTime ComputedAt);
