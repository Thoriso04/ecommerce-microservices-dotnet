using System.Net.Http.Json;
using SocEventService.Data;
using SocEventService.Models;

namespace SocEventService.Services;

public class HealthPollerService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IHttpClientFactory _httpFactory;
    private static readonly (string name, string url)[] Targets = new[]
    {
        ("user-service", "http://user-service:8080/health"),
        ("product-service", "http://product-service:8080/health"),
        ("order-service", "http://order-service:8080/health")
    };

    public HealthPollerService(IServiceProvider services, IHttpClientFactory httpFactory)
    {
        _services = services; _httpFactory = httpFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = _httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SocDbContext>();

            foreach (var (name, url) in Targets)
            {
                try
                {
                    var resp = await client.GetAsync(url, stoppingToken);
                    if (!resp.IsSuccessStatusCode)
                    {
                        db.Events.Add(new SecurityEvent
                        {
                            EventType = "SERVICE_HEALTH_FAILED",
                            Severity = "HIGH",
                            ServiceName = name,
                            Message = $"Health check for {name} returned {(int)resp.StatusCode}"
                        });
                    }
                }
                catch (Exception ex)
                {
                    db.Events.Add(new SecurityEvent
                    {
                        EventType = "SERVICE_HEALTH_FAILED",
                        Severity = "HIGH",
                        ServiceName = name,
                        Message = $"Health check for {name} failed: {ex.Message}"
                    });
                }
            }

            await db.SaveChangesAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}