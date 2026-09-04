namespace ProductService.Models;

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public record ProductRequest(string Name, string Description, decimal Price, int StockQuantity, int? Stock = null);
public record StockAdjustRequest(int Quantity);