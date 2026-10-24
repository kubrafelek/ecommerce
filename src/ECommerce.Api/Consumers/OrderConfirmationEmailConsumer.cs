using ECommerce.Api.Contracts;
using ECommerce.Api.Services;
using MassTransit;

namespace ECommerce.Api.Consumers;

public class OrderConfirmationEmailConsumer(IEmailSender emailSender, ILogger<OrderConfirmationEmailConsumer> logger)
    : IConsumer<OrderCreated>
{
    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        var order = context.Message;

        await emailSender.SendAsync(
            order.CustomerEmail,
            "Siparişiniz alındı",
            EmailTemplates.OrderCreated(order),
            context.CancellationToken);

        logger.LogInformation("Sipariş onay maili gönderildi. OrderId: {OrderId}", order.OrderId);
    }
}
