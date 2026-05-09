using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;

namespace PayFlow.Api.Tests.Auth;

public sealed class TenantIsolationTests(PayFlowApiFactory factory) : IClassFixture<PayFlowApiFactory>
{
    [Fact]
    public async Task TenantA_CannotAccessTenantBPayments()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();
        var tenantA = await client.RegisterTenantAsync($"tenant-a-{Guid.NewGuid():N}");
        var tenantB = await client.RegisterTenantAsync($"tenant-b-{Guid.NewGuid():N}");
        var tenantAPaymentId = Guid.NewGuid();
        var tenantBPaymentId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await factory.SeedAsync(async (dbContext, ct) =>
        {
            var tenantAWallet = new Wallet(Guid.NewGuid(), tenantA.TenantId, "USD", now);
            var tenantBWallet = new Wallet(Guid.NewGuid(), tenantB.TenantId, "USD", now);

            dbContext.Wallets.AddRange(tenantAWallet, tenantBWallet);
            dbContext.Payments.AddRange(
                new Payment(
                    tenantAPaymentId,
                    tenantA.TenantId,
                    tenantAWallet.Id,
                    tenantBWallet.Id,
                    tenantA.TenantId,
                    tenantB.TenantId,
                    $"idem-a-{Guid.NewGuid():N}",
                    100,
                    "USD",
                    PaymentStatus.Pending,
                    "tenant A payment",
                    null,
                    now,
                    now),
                new Payment(
                    tenantBPaymentId,
                    tenantB.TenantId,
                    tenantBWallet.Id,
                    tenantAWallet.Id,
                    tenantB.TenantId,
                    tenantA.TenantId,
                    $"idem-b-{Guid.NewGuid():N}",
                    200,
                    "USD",
                    PaymentStatus.Pending,
                    "tenant B payment",
                    null,
                    now,
                    now));

            await dbContext.SaveChangesAsync(ct);
        });

        var token = await client.IssueTokenAsync(tenantA.ApiKey, TenantRole.Developer);
        client.UseBearerToken(token.Token);

        var response = await client.GetAsync("/v1/payments", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payments = await response.Content.ReadFromJsonAsync<PaymentsListResponse>(
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        payments.Should().NotBeNull();
        payments!.Items.Select(payment => payment.PaymentId).Should().Contain(tenantAPaymentId);
        payments.Items.Select(payment => payment.PaymentId).Should().NotContain(tenantBPaymentId);
    }
}

internal sealed record PaymentsListResponse(PaymentSummaryResponse[] Items);

internal sealed record PaymentSummaryResponse(Guid PaymentId, decimal Amount, string Currency);
