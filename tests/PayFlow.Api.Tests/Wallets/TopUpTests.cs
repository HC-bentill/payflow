using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PayFlow.Api.Tests.Auth;
using PayFlow.Api.Tests.Payments;
using PayFlow.Domain.Events;

namespace PayFlow.Api.Tests.Wallets;

public sealed class TopUpTests(PayFlowApiFactory factory) : IClassFixture<PayFlowApiFactory>
{
    [Fact]
    public async Task TopUp_WithoutIdempotencyKey_ReturnsBadRequest()
    {
        await factory.ResetDatabaseAsync();
        var tenant = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        var walletId = await WalletTestClient.CreateWalletAsync(factory, tenant.TenantId);

        var response = await tenant.Client.PostAsJsonAsync(
            $"/v1/wallets/{walletId}/topup",
            new TopUpRequest(500, "USD"),
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TopUp_WithValidPayload_ReturnsCreatedWithNewBalanceAndPublishesEvent()
    {
        await factory.ResetDatabaseAsync();
        var tenant = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        var walletId = await WalletTestClient.CreateWalletAsync(factory, tenant.TenantId);

        var response = await tenant.Client.TopUpWalletAsync(walletId, $"topup-{Guid.NewGuid():N}", amount: 500);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var topUp = await response.Content.ReadFromJsonAsync<TopUpResponse>(AuthTestClient.JsonOptions, CancellationToken.None);

        topUp.Should().NotBeNull();
        topUp!.TopUpId.Should().NotBeEmpty();
        topUp.WalletId.Should().Be(walletId);
        topUp.Amount.Should().Be(500);
        topUp.NewBalance.Should().Be(500);

        factory.GetPublishedMessages<WalletToppedUpEvent>("wallet.events")
            .Should()
            .ContainSingle(@event => @event.TopUpId == topUp.TopUpId && @event.NewBalance == 500);
    }

    [Fact]
    public async Task TopUp_WithSameIdempotencyKeyTwice_ReplaysSecondResponse()
    {
        await factory.ResetDatabaseAsync();
        var tenant = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        var walletId = await WalletTestClient.CreateWalletAsync(factory, tenant.TenantId);
        var idempotencyKey = $"topup-{Guid.NewGuid():N}";

        var first = await tenant.Client.TopUpWalletAsync(walletId, idempotencyKey, amount: 500);
        var second = await tenant.Client.TopUpWalletAsync(walletId, idempotencyKey, amount: 500);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        second.Headers.TryGetValues("Idempotent-Replayed", out var values).Should().BeTrue();
        values.Should().Contain("true");

        var firstTopUp = await first.Content.ReadFromJsonAsync<TopUpResponse>(AuthTestClient.JsonOptions, CancellationToken.None);
        var secondTopUp = await second.Content.ReadFromJsonAsync<TopUpResponse>(AuthTestClient.JsonOptions, CancellationToken.None);

        secondTopUp!.TopUpId.Should().Be(firstTopUp!.TopUpId);
        secondTopUp.NewBalance.Should().Be(firstTopUp.NewBalance);
    }

    [Fact]
    public async Task TopUp_WithZeroAmount_ReturnsBadRequest()
    {
        await factory.ResetDatabaseAsync();
        var tenant = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        var walletId = await WalletTestClient.CreateWalletAsync(factory, tenant.TenantId);

        var response = await tenant.Client.TopUpWalletAsync(walletId, $"topup-{Guid.NewGuid():N}", amount: 0);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TopUp_OnAnotherTenantsWallet_ReturnsForbidden()
    {
        await factory.ResetDatabaseAsync();
        var tenantA = await PaymentTestClient.CreateAuthenticatedClientAsync(factory, $"tenant-a-{Guid.NewGuid():N}");
        var tenantB = await PaymentTestClient.CreateAuthenticatedClientAsync(factory, $"tenant-b-{Guid.NewGuid():N}");
        var tenantAWalletId = await WalletTestClient.CreateWalletAsync(factory, tenantA.TenantId);

        var response = await tenantB.Client.TopUpWalletAsync(tenantAWalletId, $"topup-{Guid.NewGuid():N}", amount: 500);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TopUp_WithWrongCurrency_ReturnsUnprocessableEntity()
    {
        await factory.ResetDatabaseAsync();
        var tenant = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        var walletId = await WalletTestClient.CreateWalletAsync(factory, tenant.TenantId, currency: "USD");

        var response = await tenant.Client.TopUpWalletAsync(walletId, $"topup-{Guid.NewGuid():N}", amount: 500, currency: "EUR");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task TopUpHistory_ReturnsPaginatedTopUpsForWallet()
    {
        await factory.ResetDatabaseAsync();
        var tenant = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        var walletId = await WalletTestClient.CreateWalletAsync(factory, tenant.TenantId);

        await tenant.Client.TopUpWalletAsync(walletId, $"topup-{Guid.NewGuid():N}", amount: 100);
        await tenant.Client.TopUpWalletAsync(walletId, $"topup-{Guid.NewGuid():N}", amount: 200);

        var response = await tenant.Client.GetAsync($"/v1/wallets/{walletId}/topups?page=1&pageSize=20", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await response.Content.ReadFromJsonAsync<TopUpHistoryResponse>(AuthTestClient.JsonOptions, CancellationToken.None);

        history.Should().NotBeNull();
        history!.Items.Should().HaveCount(2);
        history.TotalCount.Should().Be(2);
        history.Items.Sum(item => item.Amount).Should().Be(300);
        history.Items.Should().OnlyContain(item => item.WalletId == walletId && item.Status == "Completed");
    }
}
