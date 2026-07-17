using ProductService.Application.DTOs.Products;
using ProductService.Application.Models;

namespace ProductService.Application.Interfaces;

public interface IProductService
{
    Task<PagedResponse<ProductSummaryResponse>> GetAsync(ProductQueryParameters query, CancellationToken cancellationToken);
    Task<ProductResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ProductResponse> GetBySkuAsync(string sku, CancellationToken cancellationToken);
    Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken);
    Task<ProductResponse> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken);
    Task<ProductResponse> UpdateStatusAsync(Guid id, UpdateProductStatusRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid concurrencyToken, CancellationToken cancellationToken);
}
