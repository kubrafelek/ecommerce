using System.ComponentModel.DataAnnotations;

namespace ECommerce.Api.Models;

public class CreateOrderRequest
{
    [Required, EmailAddress]
    public string CustomerEmail { get; init; } = default!;

    [Required, MinLength(1)]
    public List<CreateOrderItemRequest> Items { get; init; } = new();
}

public class CreateOrderItemRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; init; }

    [Range(1, 1000)]
    public int Quantity { get; init; }
}

public record OrderResponse(
    Guid Id,
    string CustomerEmail,
    string Status,
    decimal TotalAmount,
    DateTime CreatedAt,
    string? InvoiceNumber,
    List<OrderItemResponse> Items);

public record OrderItemResponse(string ProductName, int Quantity, decimal UnitPrice);
