namespace PayFlow.Domain.Exceptions;

public sealed class InsufficientFundsException(
    Guid walletId,
    decimal requiredAmount,
    decimal availableBalance,
    string currency)
    : ApplicationException(
        $"Insufficient funds. Required: {requiredAmount} {currency}, Available: {availableBalance} {currency}")
{
    public Guid WalletId { get; } = walletId;

    public decimal RequiredAmount { get; } = requiredAmount;

    public decimal AvailableBalance { get; } = availableBalance;

    public string Currency { get; } = currency;
}
