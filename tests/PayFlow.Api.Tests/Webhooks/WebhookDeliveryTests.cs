using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using PayFlow.Api.Tests.Auth;
using PayFlow.Api.Tests.Payments;
using PayFlow.Domain.Enums;
using PayFlow.Domain.Messages;
using PayFlow.Infrastructure.Persistence;

namespace PayFlow.Api.Tests.Webhooks;

public sealed class WebhookDeliveryTests(PayFlowApiFactory factory) : IClassFixture<PayFlowApiFactory>
{
    [Fact]
    public async Task PaymentProcessed_CreatesDeliveryLogAndPublishesDeliveryJob()
    {
        await factory.ResetDatabaseAsync();
        var authenticated = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        var receiver = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        await authenticated.Client.PostAsJsonAsync(
            "/v1/webhooks/endpoints",
            new RegisterWebhookEndpointRequest("https://example.com/webhooks/payflow", "secret-min-16-chars"),
            AuthTestClient.JsonOptions,
            CancellationToken.None);

        var paymentResponse = await authenticated.Client.CreatePaymentAsync(receiver.TenantId, $"idem-{Guid.NewGuid():N}");
        paymentResponse.EnsureSuccessStatusCode();

        var log = await WaitForDeliveryLogAsync(authenticated.TenantId);
        var jobs = factory.GetPublishedMessages<WebhookDeliveryJob>("webhook.delivery");

        log.Should().NotBeNull();
        log!.Status.Should().BeOneOf(WebhookDeliveryStatus.Pending, WebhookDeliveryStatus.Delivered);
        jobs.Should().Contain(job => job.DeliveryLogId == log.Id);
    }

    private async Task<PayFlow.Domain.Entities.WebhookDeliveryLog?> WaitForDeliveryLogAsync(Guid tenantId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (DateTime.UtcNow < deadline)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PayFlowDbContext>();
            var log = await dbContext.WebhookDeliveryLogs.FirstOrDefaultAsync(
                delivery => delivery.TenantId == tenantId,
                CancellationToken.None);

            if (log is not null)
            {
                return log;
            }

            await Task.Delay(100, CancellationToken.None);
        }

        return null;
    }
}
