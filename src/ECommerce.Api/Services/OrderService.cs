using ECommerce.Api.Contracts;
using ECommerce.Api.Data;
using ECommerce.Api.Domain;
using ECommerce.Api.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Api.Services;

public interface IOrderService
{
    Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken ct);
    Task<OrderResponse?> GetAsync(Guid id, CancellationToken ct);
}

public class OrderService(AppDbContext db, IPublishEndpoint publishEndpoint) : IOrderService
{
    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken ct)
    {
        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var missing = productIds.Where(id => !products.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            throw new ProductNotFoundException(missing);

        var order = new Order
        {
            Id = NewId.NextSequentialGuid(),
            CustomerEmail = request.CustomerEmail,
            Status = OrderStatus.Created,
            CreatedAt = DateTime.UtcNow,
            Items = request.Items.Select(i => new OrderItem
            {
                ProductId = i.ProductId,
                ProductName = products[i.ProductId].Name,
                UnitPrice = products[i.ProductId].UnitPrice,
                Quantity = i.Quantity
            }).ToList()
        };
        order.TotalAmount = order.Items.Sum(i => i.UnitPrice * i.Quantity);

        db.Orders.Add(order);

        await publishEndpoint.Publish(new OrderCreated(
            order.Id,
            order.CustomerEmail,
            order.TotalAmount,
            order.CreatedAt,
            order.Items.Select(i => new OrderCreatedItem(i.ProductName, i.Quantity, i.UnitPrice)).ToList()), ct);

        await db.SaveChangesAsync(ct);

        return ToResponse(order);
    }

    public async Task<OrderResponse?> GetAsync(Guid id, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        return order is null ? null : ToResponse(order);
    }

    private static OrderResponse ToResponse(Order order) => new(
        order.Id,
        order.CustomerEmail,
        order.Status.ToString(),
        order.TotalAmount,
        order.CreatedAt,
        order.InvoiceNumber,
        order.Items.Select(i => new OrderItemResponse(i.ProductName, i.Quantity, i.UnitPrice)).ToList());
}

public class ProductNotFoundException(IEnumerable<int> productIds)
    : Exception($"Ürün bulunamadı: {string.Join(", ", productIds)}");
