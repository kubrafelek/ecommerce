using ECommerce.Api.Models;
using ECommerce.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken ct)
    {
        try
        {
            var order = await orderService.CreateAsync(request, ct);
            return AcceptedAtAction(nameof(Get), new { id = order.Id }, order);
        }
        catch (ProductNotFoundException ex)
        {
            ModelState.AddModelError(nameof(request.Items), ex.Message);
            return ValidationProblem(ModelState);
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var order = await orderService.GetAsync(id, ct);
        return order is null ? NotFound() : Ok(order);
    }
}
