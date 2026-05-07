using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using PayFlow.Api.Tests.Auth;
using PayFlow.Api.Tests.Payments;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;
using PayFlow.Domain.Messages;
using PayFlow.Infrastructure.Persistence;

namespace PayFlow.Api.Tests.Webhooks;

public sealed class ReplayWebhookTests(PayFlowApiFactory factory) : IClassFixture<PayFlowApiFactory>
{
    [Fact]
    public async Task ReplayFailedDelivery_ResetsStatusAndPublishesDeliveryJob()
    {
        await factory.ResetDatabaseAsync();
        var authenticated = await PaymentTestClient.CreateAuthenticatedClientAsync(factory);
        var endpointResponse = await authenticated.Client.PostAsJsonAsync(
            "/v1/webhooks/endpoints",
            new RegisterWebhookEndpointRequest("https://example.com/webhooks/payflow", "secret-min-16-chars"),
            AuthTestClient.JsonOptions,
            CancellationToken.None);
        var endpoint = await endpointResponse.Content.ReadFromJsonAsync<RegisterWebhookEndpointResponse>(
            AuthTestClient.JsonOptions,
            CancellationToken.None);
        var deliveryLogId = Guid.NewGuid();

        await factory.SeedAsync(async (dbContext, ct) =>
        {
            dbContext.WebhookDeliveryLogs.Add(new WebhookDeliveryLog(
                deliveryLogId,
                endpoint!.EndpointId,
                Guid.NewGuid(),
                authenticated.TenantId,
                "payment.succeeded",
                "{\"paymentId\":\"123\"}",
                WebhookDeliveryStatus.PermanentlyFailed,
                5,
                DateTime.UtcNow,
                null,
                500,
                "server error",
                DateTime.UtcNow,
                DateTime.UtcNow));

            await dbContext.SaveChangesAsync(ct);
        });

        var response = await authenticated.Client.PostAsync(
            $"/v1/webhooks/deliveries/{deliveryLogId}/replay",
            content: null,
            CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PayFlowDbContext>();
        var log = await dbContext.WebhookDeliveryLogs.FirstAsync(
            delivery => delivery.Id == deliveryLogId,
            CancellationToken.None);
        var jobs = factory.GetPublishedMessages<WebhookDeliveryJob>("webhook.delivery");

        log.Status.Should().Be(WebhookDeliveryStatus.Pending);
        log.AttemptCount.Should().Be(0);
        log.NextRetryAt.Should().BeNull();
        jobs.Should().Contain(job => job.DeliveryLogId == deliveryLogId && job.AttemptNumber == 1);
    }
}
