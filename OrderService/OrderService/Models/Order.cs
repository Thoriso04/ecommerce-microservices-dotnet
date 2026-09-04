namespace OrderService.Models;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Confirmed, Failed
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CreateOrderRequest
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public List<OrderItemRequest> Items { get; set; } = [];
}

public record OrderItemRequest(Guid ProductId, int Quantity, decimal UnitPrice);