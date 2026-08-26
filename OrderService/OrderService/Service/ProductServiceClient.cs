using System.Net.Http.Json;

namespace OrderService.Services;

public record ProductDto(Guid Id, string Name, decimal Price, int StockQuantity);

public class ProductServiceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<ProductServiceClient> _logger;
    public ProductServiceClient(HttpClient http, ILogger<ProductServiceClient> logger) { _http = http; _logger = logger; }

    public async Task<ProductDto?> GetProductAsync(Guid productId)
    {
        var resp = await _http.GetAsync($"/api/products/{productId}");
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<ProductDto>();
    }

    public async Task<bool> ReserveStockAsync(Guid productId, int quantity)
    {
        var resp = await _http.PostAsJsonAsync($"/api/products/{productId}/reserve-stock", new { Quantity = quantity });
        if (!resp.IsSuccessStatusCode)
            _logger.LogWarning("Stock reservation failed for product {ProductId}", productId);
        return resp.IsSuccessStatusCode;
    }
}