using Microsoft.AspNetCore.Mvc;
using ProductService.Application.DTOs.Categories;
using ProductService.Application.Interfaces;
using ProductService.Application.Models;

namespace ProductService.Controllers;

[ApiController]
[Route("api/v1/categories")]
[Produces("application/json")]
public sealed class CategoriesController(ICategoryService categories) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<CategorySummaryResponse>> Get([FromQuery] CategoryQueryParameters query, CancellationToken cancellationToken) => categories.GetAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<CategoryResponse> GetById(Guid id, CancellationToken cancellationToken) => categories.GetByIdAsync(id, cancellationToken);

    [HttpGet("by-slug/{slug}")]
    public Task<CategoryResponse> GetBySlug(string slug, CancellationToken cancellationToken) => categories.GetBySlugAsync(slug, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var created = await categories.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<CategoryResponse> Update(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken) => categories.UpdateAsync(id, request, cancellationToken);

    [HttpPatch("{id:guid}/status")]
    public Task<CategoryResponse> UpdateStatus(Guid id, UpdateCategoryStatusRequest request, CancellationToken cancellationToken) => categories.UpdateStatusAsync(id, request.IsActive, request.ConcurrencyToken, cancellationToken);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid concurrencyToken, CancellationToken cancellationToken)
    {
        await categories.DeleteAsync(id, concurrencyToken, cancellationToken);
        return NoContent();
    }
}
