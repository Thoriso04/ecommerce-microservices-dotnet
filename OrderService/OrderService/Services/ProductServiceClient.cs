using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace OrderService.Services;

public record ProductDto(Guid Id, string Name, decimal Price, int StockQuantity);

public class ProductServiceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<ProductServiceClient> _logger;

    public ProductServiceClient(HttpClient http, ILogger<ProductServiceClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<ProductDto?> GetProductAsync(Guid productId, string token)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/products/{productId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<ProductDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Product Service unreachable while fetching product {ProductId}", productId);
            return null;
        }
    }

    public async Task<bool> ReserveStockAsync(Guid productId, int quantity, string token)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/products/{productId}/reserve-stock")
            {
                Content = JsonContent.Create(new { Quantity = quantity })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Stock reservation failed for product {ProductId}", productId);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Product Service unreachable while reserving stock for product {ProductId}", productId);
            return false;
        }
    }
}