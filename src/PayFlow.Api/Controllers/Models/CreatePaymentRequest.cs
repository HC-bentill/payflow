namespace PayFlow.Api.Controllers.Models;

public sealed record CreatePaymentRequest(
    decimal Amount,
    string Currency,
    Guid ReceiverTenantId,
    string? Description,
    string? Metadata);
