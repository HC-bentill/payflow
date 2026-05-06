namespace PayFlow.Application.Common.Exceptions;

public sealed class NotFoundException : ApplicationException
{
    public NotFoundException(string resourceName, object id)
        : base($"{resourceName} '{id}' was not found")
    {
    }
}
