using PayFlow.Application.Wallets.Commands;

namespace PayFlow.Api.Controllers.Models;

public sealed record TopUpWalletResponse(
    Guid TopUpId,
    Guid WalletId,
    decimal Amount,
    string Currency,
    decimal NewBalance,
    DateTime CreatedAt)
{
    public static implicit operator TopUpWalletResponse(TopUpWalletResult result)
    {
        return new TopUpWalletResponse(
            result.TopUpId,
            result.WalletId,
            result.Amount,
            result.Currency,
            result.NewBalance,
            result.CreatedAt);
    }
}
