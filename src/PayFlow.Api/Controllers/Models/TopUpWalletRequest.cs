namespace PayFlow.Api.Controllers.Models;

public sealed record TopUpWalletRequest(decimal Amount, string Currency);
