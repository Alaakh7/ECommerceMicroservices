using Microsoft.AspNetCore.Mvc;
using OrderService.Application.DTOs.Orders;
using OrderService.Application.Interfaces;
using OrderService.Application.Models;

namespace OrderService.Controllers;

[ApiController]
[Route("api/v1/orders")]
[Produces("application/json")]
public sealed class OrdersController(IOrderService orders, IOrderWorkflowService workflow, IOrderCancellationService cancellation) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CreateOrderResponse>(201)] [ProducesResponseType<CreateOrderResponse>(200)] [ProducesResponseType<CreateOrderResponse>(202)]
    public async Task<ActionResult<CreateOrderResponse>> Create(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await workflow.CreateAsync(request, cancellationToken);
        if (result.StatusCode == 201) return CreatedAtAction(nameof(GetById), new { orderId = result.Value.Id }, result.Value);
        if (result.StatusCode == 202) { Response.Headers.Location = Url.ActionLink(nameof(GetById), values: new { orderId = result.Value.Id }); return Accepted(result.Value); }
        return Ok(result.Value);
    }

    [HttpGet]
    public Task<PagedResponse<OrderSummaryResponse>> Get([FromQuery] OrderQueryParameters query, CancellationToken cancellationToken) => orders.GetAsync(query, cancellationToken);

    [HttpGet("{orderId:guid}")]
    public Task<OrderDetailsResponse> GetById(Guid orderId, CancellationToken cancellationToken) => orders.GetByIdAsync(orderId, cancellationToken);

    [HttpGet("by-number/{orderNumber}")]
    public Task<OrderDetailsResponse> GetByNumber(string orderNumber, CancellationToken cancellationToken) => orders.GetByNumberAsync(orderNumber, cancellationToken);

    [HttpGet("customer/{customerId:guid}")]
    public Task<PagedResponse<OrderSummaryResponse>> GetCustomerOrders(Guid customerId, [FromQuery] CustomerOrderQueryParameters query, CancellationToken cancellationToken) => orders.GetCustomerOrdersAsync(customerId, query, cancellationToken);

    [HttpGet("{orderId:guid}/status")]
    public Task<OrderStatusResponse> GetStatus(Guid orderId, CancellationToken cancellationToken) => orders.GetStatusAsync(orderId, cancellationToken);

    [HttpPost("{orderId:guid}/cancel")]
    public async Task<ActionResult<OrderDetailsResponse>> Cancel(Guid orderId, CancelOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await cancellation.CancelAsync(orderId, request, cancellationToken);
        if (result.StatusCode == 202) { Response.Headers.Location = Url.ActionLink(nameof(GetById), values: new { orderId }); return Accepted(result.Value); }
        return Ok(result.Value);
    }

    [HttpPost("{orderId:guid}/complete")]
    public async Task<ActionResult<OrderDetailsResponse>> Complete(Guid orderId, CompleteOrderRequest request, CancellationToken cancellationToken) => Ok((await cancellation.CompleteAsync(orderId, request, cancellationToken)).Value);

    [HttpPost("{orderId:guid}/retry")]
    public async Task<ActionResult<OrderDetailsResponse>> Retry(Guid orderId, RetryOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await workflow.RetryAsync(orderId, request, cancellationToken);
        return result.StatusCode == 202 ? Accepted(result.Value) : Ok(result.Value);
    }
}
