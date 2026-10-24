namespace ECommerce.Api.Contracts;

public record OrderCreated(
    Guid OrderId,
    string CustomerEmail,
    decimal TotalAmount,
    DateTime CreatedAt,
    List<OrderCreatedItem> Items);

public record OrderCreatedItem(string ProductName, int Quantity, decimal UnitPrice);
