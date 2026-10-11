namespace SocEventService.Models;

public class SecurityEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = "";
    public string Severity { get; set; } = "LOW";
    public string ServiceName { get; set; } = "";
    public string? UserId { get; set; }
    public string? SourceIp { get; set; }
    public string? Endpoint { get; set; }
    public string? HttpMethod { get; set; }
    public int? StatusCode { get; set; }
    public string Message { get; set; } = "";
    public string? CorrelationId { get; set; }
    public string? AffectedEntity { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public record IngestEventRequest(
    string EventType, string Severity, string ServiceName, string? UserId,
    string? SourceIp, string? Endpoint, string? HttpMethod, int? StatusCode,
    string Message, string? CorrelationId, string? AffectedEntity);