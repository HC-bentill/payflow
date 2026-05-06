using Microsoft.EntityFrameworkCore.Storage;

namespace PayFlow.Application.Common;

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct);
}
