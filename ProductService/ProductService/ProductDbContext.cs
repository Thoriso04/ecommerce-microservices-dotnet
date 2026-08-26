using Microsoft.EntityFrameworkCore;
using ProductService.Models;

namespace ProductService.Data;

public class ProductDbContext : DbContext
{
    public ProductDbContext(DbContextOptions<ProductDbContext> options) : base(options) { }
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Product>().HasData(
            new Product { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Wireless Mouse", Description = "Ergonomic wireless mouse", Price = 25.99m, StockQuantity = 100 },
            new Product { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Mechanical Keyboard", Description = "RGB mechanical keyboard", Price = 79.99m, StockQuantity = 50 }
        );
    }
}