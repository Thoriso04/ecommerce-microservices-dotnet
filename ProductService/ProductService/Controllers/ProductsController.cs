using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductService.Data;
using ProductService.Models;

namespace ProductService.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController : ControllerBase
{
	private readonly ProductDbContext _db;

	public ProductsController(ProductDbContext db) => _db = db;

	[HttpGet]
	public async Task<IActionResult> GetAll() => Ok(await _db.Products.ToListAsync());

	[HttpGet("{id:guid}")]
	public async Task<IActionResult> GetById(Guid id)
	{
		var product = await _db.Products.FindAsync(id);
		return product == null ? NotFound() : Ok(product);
	}

	[HttpPost]
	public async Task<IActionResult> Create(ProductRequest request)
	{
		var product = new Product
		{
			Name = request.Name,
			Description = request.Description,
			Price = request.Price,
			StockQuantity = request.Stock ?? request.StockQuantity
		};

		_db.Products.Add(product);
		await _db.SaveChangesAsync();
		return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
	}

	[HttpPost("{id:guid}/reserve-stock")]
	public async Task<IActionResult> ReserveStock(Guid id, StockAdjustRequest request)
	{
		var product = await _db.Products.FindAsync(id);
		if (product == null || request.Quantity <= 0 || product.StockQuantity < request.Quantity)
			return BadRequest(new { message = "Insufficient stock" });

		product.StockQuantity -= request.Quantity;
		await _db.SaveChangesAsync();
		return Ok(product);
	}
}
