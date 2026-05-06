namespace PayFlow.Domain.Exceptions;

public sealed class PaymentAlreadyProcessingException : ApplicationException
{
    public PaymentAlreadyProcessingException()
        : base("A payment with this idempotency key is already being processed")
    {
    }
}
