namespace PayFlow.Application.Common.Exceptions;

public sealed class UnauthorizedException : ApplicationException
{
    public UnauthorizedException()
        : base("Unauthorized")
    {
    }
}
