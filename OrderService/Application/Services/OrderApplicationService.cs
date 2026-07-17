using OrderService.Application.DTOs.Orders;
using OrderService.Application.Interfaces;
using OrderService.Application.Models;
using OrderService.Common.Exceptions;

namespace OrderService.Application.Services;

public sealed class OrderApplicationService(IOrderRepository orders) : IOrderService
{
    public Task<PagedResponse<OrderSummaryResponse>> GetAsync(OrderQueryParameters query, CancellationToken cancellationToken) => orders.GetPagedAsync(query, cancellationToken);
    public async Task<OrderDetailsResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken) => OrderMapper.ToDetails(await orders.GetDetailsByIdAsync(id, false, cancellationToken) ?? throw new NotFoundException($"Order '{id}' was not found."));
    public async Task<OrderDetailsResponse> GetByNumberAsync(string orderNumber, CancellationToken cancellationToken) => OrderMapper.ToDetails(await orders.GetByOrderNumberAsync(orderNumber.Trim(), cancellationToken) ?? throw new NotFoundException($"Order '{orderNumber}' was not found."));
    public Task<PagedResponse<OrderSummaryResponse>> GetCustomerOrdersAsync(Guid customerId, CustomerOrderQueryParameters query, CancellationToken cancellationToken) => orders.GetCustomerOrdersAsync(customerId, query, cancellationToken);
    public async Task<OrderStatusResponse> GetStatusAsync(Guid id, CancellationToken cancellationToken) => OrderMapper.ToStatus(await orders.GetByIdAsync(id, false, cancellationToken) ?? throw new NotFoundException($"Order '{id}' was not found."));
}
