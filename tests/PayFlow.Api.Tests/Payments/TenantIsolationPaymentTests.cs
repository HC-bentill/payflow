using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PayFlow.Api.Tests.Auth;

namespace PayFlow.Api.Tests.Payments;

public sealed class TenantIsolationPaymentTests(PayFlowApiFactory factory) : IClassFixture<PayFlowApiFactory>
{
    [Fact]
    public async Task TenantB_CannotReadTenantAPayment()
    {
        await factory.ResetDatabaseAsync();
        var tenantA = await PaymentTestClient.CreateAuthenticatedClientAsync(factory, $"tenant-a-{Guid.NewGuid():N}");
        var tenantB = await PaymentTestClient.CreateAuthenticatedClientAsync(factory, $"tenant-b-{Guid.NewGuid():N}");
        var createResponse = await tenantA.Client.CreatePaymentAsync(tenantB.TenantId, $"idem-{Guid.NewGuid():N}");
        var created = await createResponse.Content.ReadFromJsonAsync<PaymentResponse>(AuthTestClient.JsonOptions, CancellationToken.None);

        var getResponse = await tenantB.Client.GetAsync($"/v1/payments/{created!.PaymentId}", CancellationToken.None);
        var listResponse = await tenantB.Client.GetAsync("/v1/payments", CancellationToken.None);

        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listResponse.Content.ReadFromJsonAsync<ListPaymentsResponse>(AuthTestClient.JsonOptions, CancellationToken.None);
        list.Should().NotBeNull();
        list!.Items.Should().BeEmpty();
        list.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Tenant_CannotQueryAnotherTenantsWalletBalance_ReturnsForbidden()
    {
        await factory.ResetDatabaseAsync();
        var tenantA = await PaymentTestClient.CreateAuthenticatedClientAsync(factory, $"tenant-a-{Guid.NewGuid():N}");
        var tenantB = await PaymentTestClient.CreateAuthenticatedClientAsync(factory, $"tenant-b-{Guid.NewGuid():N}");

        await tenantA.Client.CreatePaymentAsync(tenantB.TenantId, $"idem-{Guid.NewGuid():N}", amount: 50);

        var tenantBWallets = await tenantB.Client.GetFromJsonAsync<WalletSummaryResponse[]>("/v1/payments/wallets", AuthTestClient.JsonOptions);
        tenantBWallets.Should().NotBeNullOrEmpty();

        var response = await tenantA.Client.GetAsync($"/v1/payments/wallets/{tenantBWallets![0].WalletId}/balance", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
