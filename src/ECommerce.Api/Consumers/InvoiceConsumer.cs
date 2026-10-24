using ECommerce.Api.Contracts;
using ECommerce.Api.Data;
using ECommerce.Api.Domain;
using ECommerce.Api.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ECommerce.Api.Consumers;

public class InvoiceOptions
{
    public int SimulatedDelaySeconds { get; set; } = 10;
}

public class InvoiceConsumer(
    AppDbContext db,
    IEmailSender emailSender,
    IOptions<InvoiceOptions> options,
    ILogger<InvoiceConsumer> logger) : IConsumer<OrderCreated>
{
    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        var message = context.Message;

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == message.OrderId, context.CancellationToken);
        if (order is null)
        {
            logger.LogWarning("Fatura için sipariş bulunamadı. OrderId: {OrderId}", message.OrderId);
            return;
        }

        if (order.Status == OrderStatus.Invoiced)
            return;

        await Task.Delay(TimeSpan.FromSeconds(options.Value.SimulatedDelaySeconds), context.CancellationToken);

        order.InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{order.Id.ToString("N")[..8].ToUpperInvariant()}";
        order.InvoicedAt = DateTime.UtcNow;
        order.Status = OrderStatus.Invoiced;
        await db.SaveChangesAsync(context.CancellationToken);

        await emailSender.SendAsync(
            message.CustomerEmail,
            $"Faturanız hazır - {order.InvoiceNumber}",
            EmailTemplates.Invoice(message, order.InvoiceNumber),
            context.CancellationToken);

        logger.LogInformation("Fatura oluşturuldu ve mail gönderildi. OrderId: {OrderId}, Invoice: {Invoice}",
            order.Id, order.InvoiceNumber);
    }
}

public class InvoiceConsumerDefinition : ConsumerDefinition<InvoiceConsumer>
{
    public InvoiceConsumerDefinition()
    {
        ConcurrentMessageLimit = 16;
    }
}
