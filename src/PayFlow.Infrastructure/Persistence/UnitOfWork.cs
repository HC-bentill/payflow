using Microsoft.EntityFrameworkCore.Storage;
using PayFlow.Application.Common;

namespace PayFlow.Infrastructure.Persistence;

public sealed class UnitOfWork(PayFlowDbContext dbContext) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct)
    {
        return await dbContext.Database.BeginTransactionAsync(ct);
    }
}
