using CartService.Application.DTOs.CartItems;
using CartService.Application.DTOs.Carts;
using CartService.Application.DTOs.Checkout;
using CartService.Application.Interfaces;
using CartService.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace CartService.Controllers;

[ApiController]
[Route("api/v1/carts")]
[Produces("application/json")]
public sealed class CartsController(ICartService carts, ICartItemService items, ICartCheckoutService checkout) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CreateCartResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<CreateCartResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateCartResponse>> Create(CreateCartRequest request, CancellationToken cancellationToken)
    {
        var response = await carts.CreateOrGetAsync(request, cancellationToken);
        return response.WasCreated ? CreatedAtAction(nameof(GetById), new { cartId = response.Cart.Id }, response) : Ok(response);
    }

    [HttpGet("{cartId:guid}")]
    [ProducesResponseType<CartResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CartResponse>> GetById(Guid cartId, CancellationToken cancellationToken) =>
        Ok(await carts.GetByIdAsync(cartId, cancellationToken));

    [HttpGet("customer/{customerId:guid}/active")]
    public async Task<ActionResult<CartResponse>> GetActive(Guid customerId, CancellationToken cancellationToken) =>
        Ok(await carts.GetActiveByCustomerIdAsync(customerId, cancellationToken));

    [HttpGet("customer/{customerId:guid}")]
    public async Task<ActionResult<PagedResponse<CartSummaryResponse>>> GetHistory(Guid customerId, [FromQuery] CartQueryParameters query, CancellationToken cancellationToken) =>
        Ok(await carts.GetCustomerHistoryAsync(customerId, query, cancellationToken));

    [HttpPost("{cartId:guid}/items")]
    public async Task<ActionResult<CartResponse>> AddItem(Guid cartId, AddCartItemRequest request, CancellationToken cancellationToken) =>
        Ok(await items.AddAsync(cartId, request, cancellationToken));

    [HttpPut("{cartId:guid}/items/{productId:guid}")]
    public async Task<ActionResult<CartResponse>> UpdateItem(Guid cartId, Guid productId, UpdateCartItemQuantityRequest request, CancellationToken cancellationToken) =>
        Ok(await items.UpdateQuantityAsync(cartId, productId, request, cancellationToken));

    [HttpDelete("{cartId:guid}/items/{productId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveItem(Guid cartId, Guid productId, [FromQuery] Guid concurrencyToken, CancellationToken cancellationToken)
    {
        await items.RemoveAsync(cartId, productId, concurrencyToken, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{cartId:guid}/items")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Clear(Guid cartId, [FromQuery] Guid concurrencyToken, CancellationToken cancellationToken)
    {
        await items.ClearAsync(cartId, concurrencyToken, cancellationToken);
        return NoContent();
    }

    [HttpPost("{cartId:guid}/refresh")]
    public async Task<ActionResult<RefreshCartResponse>> Refresh(Guid cartId, RefreshCartRequest request, CancellationToken cancellationToken) =>
        Ok(await items.RefreshAsync(cartId, request, cancellationToken));

    [HttpPost("{cartId:guid}/validate")]
    public async Task<ActionResult<ValidateCartResponse>> Validate(Guid cartId, ValidateCartRequest request, CancellationToken cancellationToken) =>
        Ok(await items.ValidateAsync(cartId, request, cancellationToken));

    [HttpPost("{cartId:guid}/checkout/prepare")]
    public async Task<ActionResult<PrepareCheckoutResponse>> Prepare(Guid cartId, PrepareCheckoutRequest request, CancellationToken cancellationToken) =>
        Ok(await checkout.PrepareAsync(cartId, request, cancellationToken));

    [HttpPost("{cartId:guid}/checkout/complete")]
    public async Task<ActionResult<CompleteCheckoutResponse>> Complete(Guid cartId, CompleteCheckoutRequest request, CancellationToken cancellationToken) =>
        Ok(await checkout.CompleteAsync(cartId, request, cancellationToken));

    [HttpPost("{cartId:guid}/checkout/cancel")]
    public async Task<ActionResult<CartResponse>> Cancel(Guid cartId, CancelCheckoutRequest request, CancellationToken cancellationToken) =>
        Ok(await checkout.CancelAsync(cartId, request, cancellationToken));

    [HttpPost("{cartId:guid}/abandon")]
    public async Task<ActionResult<CartResponse>> Abandon(Guid cartId, AbandonCartRequest request, CancellationToken cancellationToken) =>
        Ok(await carts.AbandonAsync(cartId, request, cancellationToken));
}
