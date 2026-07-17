using ProductService.Domain.Entities;

namespace ProductService.Application.Interfaces;

public interface IInventoryTransactionRepository
{
    IQueryable<InventoryTransaction> Query();
    Task<InventoryTransaction?> GetByOperationIdAsync(string operationId, CancellationToken cancellationToken);
    Task AddAsync(InventoryTransaction transaction, CancellationToken cancellationToken);
}
