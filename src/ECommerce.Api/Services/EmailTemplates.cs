using System.Globalization;
using System.Text;
using ECommerce.Api.Contracts;

namespace ECommerce.Api.Services;

public static class EmailTemplates
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static string OrderCreated(OrderCreated order)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Siparişiniz oluşturuldu.");
        sb.AppendLine("Sipariş Detayları;");
        AppendItems(sb, order.Items);
        return sb.ToString();
    }

    public static string Invoice(OrderCreated order, string invoiceNumber)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Fatura No: {invoiceNumber}");
        sb.AppendLine($"Sipariş No: {order.OrderId}");
        sb.AppendLine("Sipariş Detayları;");
        AppendItems(sb, order.Items);
        sb.AppendLine($"Toplam Tutar: {order.TotalAmount.ToString("N2", Tr)} TL");
        return sb.ToString();
    }

    private static void AppendItems(StringBuilder sb, IEnumerable<OrderCreatedItem> items)
    {
        foreach (var item in items)
        {
            sb.AppendLine();
            sb.AppendLine($"• Ürün adı: {item.ProductName}");
            sb.AppendLine($"• Adet: {item.Quantity}");
            sb.AppendLine($"• Birim Fiyat: {item.UnitPrice.ToString("N2", Tr)} TL");
        }
    }
}
