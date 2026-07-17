using Microsoft.AspNetCore.Mvc;
using ProductService.Application.DTOs.Products;
using ProductService.Application.Interfaces;
using ProductService.Application.Models;

namespace ProductService.Controllers;

[ApiController]
[Route("api/v1/products")]
[Produces("application/json")]
public sealed class ProductsController(IProductService products) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<ProductSummaryResponse>>(StatusCodes.Status200OK)]
    public Task<PagedResponse<ProductSummaryResponse>> Get([FromQuery] ProductQueryParameters query, CancellationToken cancellationToken) => products.GetAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ProductResponse> GetById(Guid id, CancellationToken cancellationToken) => products.GetByIdAsync(id, cancellationToken);

    [HttpGet("by-sku/{sku}")]
    public Task<ProductResponse> GetBySku(string sku, CancellationToken cancellationToken) => products.GetBySkuAsync(sku, cancellationToken);

    [HttpPost]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ProductResponse>> Create(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var created = await products.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<ProductResponse> Update(Guid id, UpdateProductRequest request, CancellationToken cancellationToken) => products.UpdateAsync(id, request, cancellationToken);

    [HttpPatch("{id:guid}/status")]
    public Task<ProductResponse> UpdateStatus(Guid id, UpdateProductStatusRequest request, CancellationToken cancellationToken) => products.UpdateStatusAsync(id, request, cancellationToken);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid concurrencyToken, CancellationToken cancellationToken)
    {
        await products.DeleteAsync(id, concurrencyToken, cancellationToken);
        return NoContent();
    }
}
