namespace ECommerce.Api.Domain;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public decimal UnitPrice { get; set; }
}

public enum OrderStatus
{
    Created = 0,
    Invoiced = 1
}

public class Order
{
    public Guid Id { get; set; }
    public string CustomerEmail { get; set; } = default!;
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime? InvoicedAt { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public int Id { get; set; }
    public Guid OrderId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
