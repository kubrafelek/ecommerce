using ECommerce.Api.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.UnitPrice).HasPrecision(18, 2);
            e.HasData(
                new Product { Id = 1, Name = "Laptop", UnitPrice = 35000m },
                new Product { Id = 2, Name = "Kulaklık", UnitPrice = 1500m },
                new Product { Id = 3, Name = "Mouse", UnitPrice = 450m });
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.Property(o => o.CustomerEmail).HasMaxLength(256).IsRequired();
            e.Property(o => o.TotalAmount).HasPrecision(18, 2);
            e.Property(o => o.InvoiceNumber).HasMaxLength(50);
            e.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId);
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
            e.Property(i => i.UnitPrice).HasPrecision(18, 2);
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
