using System.Net.Http.Json;

namespace OrderService.Services;

public class SecurityEventPublisher
{
    private readonly HttpClient _http;
    private readonly ILogger<SecurityEventPublisher> _logger;
    public SecurityEventPublisher(HttpClient http, ILogger<SecurityEventPublisher> logger) { _http = http; _logger = logger; }

    public async Task PublishAsync(string eventType, string severity, string serviceName, string message,
        string? userId = null, string? sourceIp = null, string? endpoint = null, string? httpMethod = null, int? statusCode = null)
    {
        try
        {
            await _http.PostAsJsonAsync("/api/events", new
            {
                EventType = eventType,
                Severity = severity,
                ServiceName = serviceName,
                UserId = userId,
                SourceIp = sourceIp,
                Endpoint = endpoint,
                HttpMethod = httpMethod,
                StatusCode = statusCode,
                Message = message,
                CorrelationId = (string?)null,
                AffectedEntity = (string?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish security event to SOC service");
        }
    }
}