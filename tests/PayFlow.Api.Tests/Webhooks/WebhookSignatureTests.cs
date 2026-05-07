using FluentAssertions;
using PayFlow.Infrastructure.Services;

namespace PayFlow.Api.Tests.Webhooks;

public sealed class WebhookSignatureTests
{
    [Fact]
    public void ComputeSignature_WithKnownPayloadAndSecret_ReturnsExpectedHmac()
    {
        var signature = WebhookSignatureService.ComputeSignature(
            "{\"paymentId\":\"123\",\"amount\":100}",
            "test-secret-123456");

        signature.Should().Be("4409a688c6ce8bbc80873a4056e705f80d5b801e30f013eeab72af93451497e5");
    }

    [Fact]
    public void ComputeSignature_ChangesWhenPayloadChanges()
    {
        var original = WebhookSignatureService.ComputeSignature(
            "{\"paymentId\":\"123\",\"amount\":100}",
            "test-secret-123456");
        var changed = WebhookSignatureService.ComputeSignature(
            "{\"paymentId\":\"123\",\"amount\":101}",
            "test-secret-123456");

        changed.Should().NotBe(original);
    }

    [Fact]
    public void ComputeSignature_ChangesWhenSecretChanges()
    {
        var original = WebhookSignatureService.ComputeSignature(
            "{\"paymentId\":\"123\",\"amount\":100}",
            "test-secret-123456");
        var changed = WebhookSignatureService.ComputeSignature(
            "{\"paymentId\":\"123\",\"amount\":100}",
            "different-secret-123456");

        changed.Should().NotBe(original);
    }
}
