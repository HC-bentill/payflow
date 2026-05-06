namespace PayFlow.Application.Common.Exceptions;

public sealed class TenantAlreadyExistsException : ApplicationException
{
    public TenantAlreadyExistsException()
        : base("Tenant name already taken")
    {
    }
}
