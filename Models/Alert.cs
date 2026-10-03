namespace SocEventService.Models;

public class Alert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RuleName { get; set; } = "";
    public string Severity { get; set; } = "MEDIUM";
    public string Status { get; set; } = "OPEN";
    public string Description { get; set; } = "";
    public string RelatedEventIds { get; set; } = "";
    public string? AffectedService { get; set; }
    public string? AffectedUser { get; set; }
    public string? AffectedEntity { get; set; }
    public string RecommendedAction { get; set; } = "";
    public string ThreatName { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}