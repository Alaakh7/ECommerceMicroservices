using Microsoft.EntityFrameworkCore;
using ProductService.Application.Interfaces;
using ProductService.Domain.Entities;
using ProductService.Infrastructure.Data;

namespace ProductService.Infrastructure.Repositories;

public sealed class InventoryTransactionRepository(ProductDbContext dbContext) : IInventoryTransactionRepository
{
    public IQueryable<InventoryTransaction> Query() => dbContext.InventoryTransactions.AsNoTracking();

    public Task<InventoryTransaction?> GetByOperationIdAsync(string operationId, CancellationToken cancellationToken) =>
        Query().SingleOrDefaultAsync(x => x.OperationId == operationId, cancellationToken);

    public Task AddAsync(InventoryTransaction transaction, CancellationToken cancellationToken) =>
        dbContext.InventoryTransactions.AddAsync(transaction, cancellationToken).AsTask();
}
