using Microsoft.EntityFrameworkCore;
using SocEventService.Data;
using SocEventService.Models;

namespace SocEventService.Services;

public class DetectionRuleEngine : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DetectionRuleEngine> _logger;

    public DetectionRuleEngine(IServiceProvider services, ILogger<DetectionRuleEngine> logger)
    {
        _services = services; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SocDbContext>();
                var graph = scope.ServiceProvider.GetRequiredService<GraphService>();

                await RunFailedLoginRule(db, graph);
                await RunUnauthorizedAccessRule(db, graph);
                await RunServiceFailureRule(db, graph);
                await RunAbnormalRequestRateRule(db, graph);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Detection rule engine iteration failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
    }

    private async Task RunFailedLoginRule(SocDbContext db, GraphService graph)
    {
        var window = DateTime.UtcNow.AddMinutes(-5);
        var groups = (await db.Events
            .Where(e => e.EventType == "AUTH_FAILED" && e.Timestamp >= window)
            .ToListAsync())
            .GroupBy(e => e.UserId)
            .Where(g => g.Count() >= 5)
            .ToList();
            

        foreach (var g in groups)
        {
            var username = g.Key ?? "unknown";
            var exists = await db.Alerts.AnyAsync(a => a.RuleName == "Multiple Failed Logins"
                && a.AffectedUser == username && a.Timestamp >= window);
            if (exists) continue;

            var events = g.OrderByDescending(e => e.Timestamp).Take(5).ToList();
            var alert = new Alert
            {
                RuleName = "Multiple Failed Logins",
                Severity = "HIGH",
                Status = "OPEN",
                Description = $"Five or more failed login attempts detected for user '{username}' within five minutes.",
                RelatedEventIds = string.Join(",", events.Select(e => e.Id)),
                AffectedService = "user-service",
                AffectedUser = username,
                AffectedEntity = "login-endpoint",
                RecommendedAction = "Temporarily lock the account, require MFA, and verify whether credentials were compromised.",
                ThreatName = "CredentialAttack"
            };
            db.Alerts.Add(alert);
            await db.SaveChangesAsync();
            await PushAlertToGraph(graph, alert, events, username);
            _logger.LogWarning("ALERT created: {RuleName} for user {User}", alert.RuleName, username);
        }
    }

    private async Task RunUnauthorizedAccessRule(SocDbContext db, GraphService graph)
    {
        var window = DateTime.UtcNow.AddMinutes(-5);
        var groups = (await db.Events
            .Where(e => e.EventType == "UNAUTHORISED_ACCESS" && e.Timestamp >= window)
            .ToListAsync())
            .GroupBy(e => new { e.UserId, e.SourceIp })
            .Where(g => g.Count() >= 3)
            .ToList();

        foreach (var g in groups)
        {
            var who = g.Key.UserId ?? g.Key.SourceIp ?? "unknown";
            var exists = await db.Alerts.AnyAsync(a => a.RuleName == "Unauthorised Endpoint Access"
                && a.AffectedUser == who && a.Timestamp >= window);
            if (exists) continue;

            var events = g.OrderByDescending(e => e.Timestamp).Take(5).ToList();
            var alert = new Alert
            {
                RuleName = "Unauthorised Endpoint Access",
                Severity = "MEDIUM",
                Status = "OPEN",
                Description = $"Repeated 401/403 responses detected for '{who}' on protected endpoints.",
                RelatedEventIds = string.Join(",", events.Select(e => e.Id)),
                AffectedService = events.FirstOrDefault()?.ServiceName,
                AffectedUser = who,
                AffectedEntity = events.FirstOrDefault()?.Endpoint,
                RecommendedAction = "Review access logs, confirm the user's role permissions, and consider blocking the source IP if abuse continues.",
                ThreatName = "UnauthorizedAccess"
            };
            db.Alerts.Add(alert);
            await db.SaveChangesAsync();
            await PushAlertToGraph(graph, alert, events, who);
            _logger.LogWarning("ALERT created: {RuleName} for {Who}", alert.RuleName, who);
        }
    }

    private async Task RunServiceFailureRule(SocDbContext db, GraphService graph)
    {
        var window = DateTime.UtcNow.AddMinutes(-5);
        var groups = (await db.Events
            .Where(e => (e.EventType == "SERVICE_ERROR" || e.EventType == "SERVICE_HEALTH_FAILED") && e.Timestamp >= window)
            .ToListAsync())
            .GroupBy(e => e.ServiceName)
            .Where(g => g.Count() >= 3)
            .ToList();

        foreach (var g in groups)
        {
            var service = g.Key;
            var exists = await db.Alerts.AnyAsync(a => a.RuleName == "Service Failure"
                && a.AffectedService == service && a.Timestamp >= window);
            if (exists) continue;

            var events = g.OrderByDescending(e => e.Timestamp).Take(5).ToList();
            var alert = new Alert
            {
                RuleName = "Service Failure",
                Severity = "HIGH",
                Status = "OPEN",
                Description = $"Repeated failures or health check issues detected for service '{service}'.",
                RelatedEventIds = string.Join(",", events.Select(e => e.Id)),
                AffectedService = service,
                AffectedEntity = service,
                RecommendedAction = "Investigate service logs, verify dependency health, and consider restarting or scaling the affected service.",
                ThreatName = "ServiceDependencyFailure"
            };
            db.Alerts.Add(alert);
            await db.SaveChangesAsync();
            await PushAlertToGraph(graph, alert, events, null);
            _logger.LogWarning("ALERT created: {RuleName} for {Service}", alert.RuleName, service);
        }
    }

    private async Task RunAbnormalRequestRateRule(SocDbContext db, GraphService graph)
    {
        var window = DateTime.UtcNow.AddMinutes(-1);
        var groups = (await db.Events
            .Where(e => e.EventType == "API_REQUEST" && e.Timestamp >= window)
            .ToListAsync())
            .GroupBy(e => e.SourceIp)
            .Where(g => g.Count() >= 50)
            .ToList();

        foreach (var g in groups)
        {
            var ip = g.Key ?? "unknown";
            var exists = await db.Alerts.AnyAsync(a => a.RuleName == "Abnormal Request Rate"
                && a.AffectedEntity == ip && a.Timestamp >= window);
            if (exists) continue;

            var events = g.OrderByDescending(e => e.Timestamp).Take(5).ToList();
            var alert = new Alert
            {
                RuleName = "Abnormal Request Rate",
                Severity = "MEDIUM",
                Status = "OPEN",
                Description = $"Unusually high request volume ({g.Count()} requests in 1 minute) detected from source '{ip}'.",
                RelatedEventIds = string.Join(",", events.Select(e => e.Id)),
                AffectedService = "api-gateway",
                AffectedEntity = ip,
                RecommendedAction = "Apply or tighten rate limiting for this source and monitor for denial-of-service behaviour.",
                ThreatName = "DenialOfService"
            };
            db.Alerts.Add(alert);
            await db.SaveChangesAsync();
            await PushAlertToGraph(graph, alert, events, null);
            _logger.LogWarning("ALERT created: {RuleName} from {Ip}", alert.RuleName, ip);
        }
    }

    private async Task PushAlertToGraph(GraphService graph, Alert alert, List<SecurityEvent> events, string? userId)
    {
        foreach (var ev in events)
        {
            await graph.RunWriteAsync(@"
                MERGE (e:Event {eventId:$eventId})
                SET e.eventType=$eventType, e.timestamp=$timestamp, e.message=$message
                MERGE (s:Service {name:$serviceName})
                MERGE (e)-[:GENERATED_BY]->(s)
                WITH e
                FOREACH (_ IN CASE WHEN $endpoint IS NULL THEN [] ELSE [1] END |
                    MERGE (ep:Endpoint {path:$endpoint})
                    MERGE (e)-[:TARGETED]->(ep)
                )
                FOREACH (_ IN CASE WHEN $userId IS NULL THEN [] ELSE [1] END |
                    MERGE (u:User {userId:$userId})
                    MERGE (u)-[:TRIGGERED]->(e)
                )
            ", new
            {
                eventId = ev.Id.ToString(),
                eventType = ev.EventType,
                timestamp = ev.Timestamp.ToString("o"),
                message = ev.Message,
                serviceName = ev.ServiceName,
                endpoint = ev.Endpoint,
                userId = ev.UserId
            });
        }

        await graph.RunWriteAsync(@"
            MERGE (a:Alert {alertId:$alertId})
            SET a.ruleName=$ruleName, a.severity=$severity, a.status=$status,
                a.description=$description, a.timestamp=$timestamp,
                a.recommendedAction=$recommendedAction
            WITH a
            UNWIND $eventIds AS eid
            MATCH (e:Event {eventId:eid})
            MERGE (e)-[:CREATED_ALERT]->(a)
            WITH a
            MATCH (t:Threat {name:$threatName})
            MERGE (a)-[:INDICATES]->(t)
            WITH a
            FOREACH (_ IN CASE WHEN $affectedService IS NULL THEN [] ELSE [1] END |
                MERGE (s:Service {name:$affectedService})
                MERGE (a)-[:AFFECTS]->(s)
            )
        ", new
        {
            alertId = alert.Id.ToString(),
            ruleName = alert.RuleName,
            severity = alert.Severity,
            status = alert.Status,
            description = alert.Description,
            timestamp = alert.Timestamp.ToString("o"),
            recommendedAction = alert.RecommendedAction,
            eventIds = events.Select(e => e.Id.ToString()).ToArray(),
            threatName = alert.ThreatName,
            affectedService = alert.AffectedService
        });

        if (!string.IsNullOrEmpty(alert.AffectedService))
        {
            await graph.RunWriteAsync(@"
                MATCH (a:Alert {alertId:$alertId})
                MERGE (i:Incident {name:$incidentName})
                MERGE (i)-[:CONTAINS]->(a)
            ", new { alertId = alert.Id.ToString(), incidentName = $"incident-{alert.AffectedService}" });
        }
    }
}