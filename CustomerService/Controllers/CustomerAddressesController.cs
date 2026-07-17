using CustomerService.Application.DTOs.Addresses;
using CustomerService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CustomerService.Controllers;

[ApiController]
[Route("api/v1/customers/{customerId:guid}/addresses")]
public sealed class CustomerAddressesController(ICustomerAddressService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerAddressResponse>>> Get(Guid customerId, CancellationToken cancellationToken) => Ok(await service.GetAsync(customerId, cancellationToken));

    [HttpGet("{addressId:guid}")]
    public async Task<ActionResult<CustomerAddressResponse>> GetById(Guid customerId, Guid addressId, CancellationToken cancellationToken) => Ok(await service.GetByIdAsync(customerId, addressId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<CustomerAddressResponse>> Create(Guid customerId, CreateCustomerAddressRequest request, CancellationToken cancellationToken)
    {
        var response = await service.CreateAsync(customerId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { customerId, addressId = response.Id }, response);
    }

    [HttpPut("{addressId:guid}")]
    public async Task<ActionResult<CustomerAddressResponse>> Update(Guid customerId, Guid addressId, UpdateCustomerAddressRequest request, CancellationToken cancellationToken) => Ok(await service.UpdateAsync(customerId, addressId, request, cancellationToken));

    [HttpPatch("{addressId:guid}/default")]
    public async Task<ActionResult<CustomerAddressResponse>> SetDefault(Guid customerId, Guid addressId, SetDefaultAddressRequest request, CancellationToken cancellationToken) => Ok(await service.SetDefaultAsync(customerId, addressId, request, cancellationToken));

    [HttpDelete("{addressId:guid}")]
    public async Task<IActionResult> Delete(Guid customerId, Guid addressId, [FromQuery] Guid concurrencyToken, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(customerId, addressId, concurrencyToken, cancellationToken);
        return NoContent();
    }
}
