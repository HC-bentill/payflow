namespace PayFlow.Domain.Exceptions;

public sealed class InvalidCurrencyException : ApplicationException
{
    public InvalidCurrencyException()
        : base("Currency must be a valid ISO 4217 code (e.g. USD, GHS, EUR)")
    {
    }
}
