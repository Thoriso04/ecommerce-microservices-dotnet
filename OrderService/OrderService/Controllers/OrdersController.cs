using System.Security.Claims;
using OrderService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Models;
using OrderService.Services;

namespace OrderService.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly OrderDbContext _db;
    private readonly ProductServiceClient _productClient;
    private readonly ILogger<OrdersController> _logger;
    private readonly SecurityEventPublisher _eventPublisher;

    public OrdersController(OrderDbContext db, ProductServiceClient productClient, ILogger<OrdersController> logger, SecurityEventPublisher eventPublisher)
    {
        _db = db;
        _productClient = productClient;
        _logger = logger;
        _eventPublisher = eventPublisher;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderRequest req)
    {
        var item = req.Items.FirstOrDefault();
        var productId = item?.ProductId ?? req.ProductId;
        var quantity = item?.Quantity ?? req.Quantity;
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub")!.Value);

        var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

        var product = await _productClient.GetProductAsync(productId, token);
        if (product == null)
        {
            _logger.LogWarning("Order creation failed: product {ProductId} not found", productId);
            await _eventPublisher.PublishAsync("SERVICE_ERROR", "HIGH", "order-service",
                $"Order Service could not reach Product Service for product {productId}",
                userId: userId.ToString());
            return BadRequest(new { message = "Product not found or Product Service unavailable" });
        }

        var reserved = await _productClient.ReserveStockAsync(productId, quantity, token);
        if (!reserved)
        {
            _logger.LogWarning("Order creation failed: could not reserve stock for product {ProductId}", req.ProductId);
            return BadRequest(new { message = "Could not reserve stock (insufficient quantity or service error)" });
        }

        var order = new Order
        {
            UserId = userId,
            ProductId = product.Id,
            ProductName = product.Name,
            Quantity = quantity,
            TotalPrice = product.Price * quantity,
            Status = "Confirmed"
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Order {OrderId} created for user {UserId}: {Quantity}x {ProductName}", order.Id, userId, req.Quantity, product.Name);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var o = await _db.Orders.FindAsync(id);
        return o == null ? NotFound() : Ok(o);
    }

    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub")!.Value);
        return Ok(await _db.Orders.Where(o => o.UserId == userId).ToListAsync());
    }

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetByUser(Guid userId) =>
        Ok(await _db.Orders.Where(o => o.UserId == userId).ToListAsync());
}