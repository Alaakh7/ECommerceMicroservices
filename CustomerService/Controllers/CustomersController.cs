using System.ComponentModel.DataAnnotations;
using CustomerService.Application.DTOs.Customers;
using CustomerService.Application.Interfaces;
using CustomerService.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace CustomerService.Controllers;

[ApiController]
[Route("api/v1/customers")]
public sealed class CustomersController(ICustomerService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<CustomerSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<CustomerSummaryResponse>>> Get([FromQuery] CustomerQueryParameters query, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CustomerDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDetailsResponse>> GetById(Guid id, CancellationToken cancellationToken) => Ok(await service.GetByIdAsync(id, cancellationToken));

    [HttpGet("by-number/{customerNumber}")]
    public async Task<ActionResult<CustomerDetailsResponse>> GetByNumber(string customerNumber, CancellationToken cancellationToken) => Ok(await service.GetByCustomerNumberAsync(customerNumber, cancellationToken));

    [HttpGet("by-email")]
    public async Task<ActionResult<CustomerDetailsResponse>> GetByEmail([FromQuery, Required, EmailAddress] string email, CancellationToken cancellationToken) => Ok(await service.GetByEmailAsync(email, cancellationToken));

    [HttpPost]
    [ProducesResponseType<CustomerDetailsResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CustomerDetailsResponse>> Create(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var response = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerDetailsResponse>> Update(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken) => Ok(await service.UpdateAsync(id, request, cancellationToken));

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<CustomerDetailsResponse>> UpdateStatus(Guid id, UpdateCustomerStatusRequest request, CancellationToken cancellationToken) => Ok(await service.UpdateStatusAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid concurrencyToken, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, concurrencyToken, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/eligibility")]
    public async Task<ActionResult<CustomerEligibilityResponse>> GetEligibility(Guid id, CancellationToken cancellationToken) => Ok(await service.GetEligibilityAsync(id, cancellationToken));

    [HttpPost("eligibility/batch")]
    public async Task<ActionResult<BatchCustomerEligibilityResponse>> GetBatchEligibility(BatchCustomerEligibilityRequest request, CancellationToken cancellationToken) => Ok(await service.GetBatchEligibilityAsync(request, cancellationToken));
}
