namespace PayFlow.Application.Common.Exceptions;

public sealed class UnauthorizedException : ApplicationException
{
    public UnauthorizedException()
        : base("Unauthorized")
    {
    }

    public UnauthorizedException(string message, int statusCode = 401)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; } = 401;
}
