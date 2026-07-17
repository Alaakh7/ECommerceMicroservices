using ProductService.Application.DTOs.Categories;
using ProductService.Application.Models;

namespace ProductService.Application.Interfaces;

public interface ICategoryService
{
    Task<PagedResponse<CategorySummaryResponse>> GetAsync(CategoryQueryParameters query, CancellationToken cancellationToken);
    Task<CategoryResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<CategoryResponse> GetBySlugAsync(string slug, CancellationToken cancellationToken);
    Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken);
    Task<CategoryResponse> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken);
    Task<CategoryResponse> UpdateStatusAsync(Guid id, bool isActive, Guid concurrencyToken, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid concurrencyToken, CancellationToken cancellationToken);
}
