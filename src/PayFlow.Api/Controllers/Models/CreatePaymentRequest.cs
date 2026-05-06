namespace PayFlow.Api.Controllers.Models;

public sealed record CreatePaymentRequest(
    decimal Amount,
    string Currency,
    string? Description,
    string? Metadata);
