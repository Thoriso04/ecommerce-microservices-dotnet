using Neo4j.Driver;

namespace SocEventService.Services;

public class RagService
{
    private readonly GraphService _graph;

    public RagService(GraphService graph) { _graph = graph; }

    public async Task<object> AnswerAsync(string question)
    {
        var q = question.ToLowerInvariant();

        if (q.Contains("why") && q.Contains("alert"))
        {
            var alertId = ExtractAlertId(question);
            if (alertId != null) return await ExplainAlert(alertId, question);
        }

        if (q.Contains("control") || q.Contains("mitigate") || q.Contains("response"))
        {
            var threat = ExtractThreatName(q);
            if (threat != null) return await ControlsForThreat(threat, question);
        }

        if (q.Contains("affected") || q.Contains("impact") || q.Contains("depend"))
        {
            var service = ExtractServiceName(q);
            if (service != null) return await DependencyImpact(service, question);
        }

        if (q.Contains("user") && q.Contains("failed login"))
        {
            return await UsersWithFailedLogins(question);
        }

        return new
        {
            question,
            answer = "This question could not be mapped to a known retrieval pattern. Try asking about a specific alert ID, a threat, a service, or failed logins.",
            retrievedNodes = Array.Empty<object>(),
            retrievedRelationships = Array.Empty<object>(),
            retrievalMethod = "none"
        };
    }

    private string? ExtractAlertId(string question)
    {
        var words = question.Split(' ', '\n', '\t');
        return words.FirstOrDefault(w => w.StartsWith("alert-", StringComparison.OrdinalIgnoreCase))
               ?? words.FirstOrDefault(w => Guid.TryParse(w.Trim('.', ',', '?'), out _));
    }

    private string? ExtractThreatName(string q)
    {
        if (q.Contains("credential") || q.Contains("brute force") || q.Contains("login")) return "CredentialAttack";
        if (q.Contains("unauthor")) return "UnauthorizedAccess";
        if (q.Contains("depend") || q.Contains("cascad")) return "ServiceDependencyFailure";
        if (q.Contains("denial") || q.Contains("dos") || q.Contains("rate")) return "DenialOfService";
        return null;
    }

    private string? ExtractServiceName(string q)
    {
        if (q.Contains("user-service") || q.Contains("user service")) return "user-service";
        if (q.Contains("product-service") || q.Contains("product service")) return "product-service";
        if (q.Contains("order-service") || q.Contains("order service")) return "order-service";
        if (q.Contains("gateway")) return "api-gateway";
        return null;
    }

    private async Task<object> ExplainAlert(string alertId, string question)
    {
        var rows = await _graph.RunReadAsync(@"
            MATCH (a:Alert {alertId:$alertId})
            OPTIONAL MATCH (e:Event)-[:CREATED_ALERT]->(a)
            OPTIONAL MATCH (a)-[:INDICATES]->(t:Threat)
            OPTIONAL MATCH (c:Control)-[:MITIGATES]->(t)
            RETURN a.ruleName AS ruleName, a.description AS description,
                   a.recommendedAction AS recommendedAction,
                   t.name AS threatName,
                   collect(DISTINCT c.name) AS controls
        ", new { alertId });

        if (rows.Count == 0)
            return new { question, answer = $"No alert found with id '{alertId}'.", retrievedNodes = Array.Empty<object>(), retrievalMethod = "cypher-graph-traversal" };

        var r = rows[0];
        var controls = r["controls"].As<List<object>>().Select(x => x.ToString()).Where(x => !string.IsNullOrEmpty(x)).ToList();
        var controlList = controls.Count > 0 ? string.Join(", ", controls) : "no specific controls recorded";

        var answer = $"Alert {alertId} was created by rule '{r["ruleName"]}'. {r["description"]} " +
                     $"This is linked to the threat '{r["threatName"]}'. Recommended controls are {controlList}. " +
                     $"Suggested response: {r["recommendedAction"]}";

        return new
        {
            question,
            answer,
            retrievedNodes = new object[] { new { type = "Alert", id = alertId }, new { type = "Threat", name = r["threatName"]?.ToString() } },
            retrievedRelationships = new[] { "Event-CREATED_ALERT->Alert", "Alert-INDICATES->Threat", "Control-MITIGATES->Threat" },
            retrievalMethod = "cypher-graph-traversal (template-based generation)"
        };
    }

    private async Task<object> ControlsForThreat(string threatName, string question)
    {
        var rows = await _graph.RunReadAsync(@"
            MATCH (c:Control)-[:MITIGATES]->(t:Threat {name:$threatName})
            RETURN c.name AS control, c.description AS description
        ", new { threatName });

        var controls = rows.Select(r => r["control"].As<string>()).ToList();
        var answer = controls.Count > 0
            ? $"{threatName} is mitigated by: {string.Join(", ", controls)}."
            : $"No controls were found in the graph for {threatName}.";

        return new
        {
            question,
            answer,
            retrievedNodes = rows.Select(r => new { type = "Control", name = r["control"].As<string>() }),
            retrievedRelationships = new[] { "Control-MITIGATES->Threat" },
            retrievalMethod = "cypher-graph-traversal (template-based generation)"
        };
    }

    private async Task<object> DependencyImpact(string serviceName, string question)
    {
        var rows = await _graph.RunReadAsync(@"
            MATCH (dependent:Service)-[:DEPENDS_ON]->(s:Service {name:$serviceName})
            RETURN dependent.name AS dependentService
        ", new { serviceName });

        var dependents = rows.Select(r => r["dependentService"].As<string>()).ToList();
        var answer = dependents.Count > 0
            ? $"If {serviceName} fails, the following services would be directly impacted: {string.Join(", ", dependents)}."
            : $"No services in the graph currently depend on {serviceName}.";

        return new
        {
            question,
            answer,
            retrievedNodes = rows.Select(r => new { type = "Service", name = r["dependentService"].As<string>() }),
            retrievedRelationships = new[] { "Service-DEPENDS_ON->Service" },
            retrievalMethod = "cypher-graph-traversal (template-based generation)"
        };
    }

    private async Task<object> UsersWithFailedLogins(string question)
    {
        var rows = await _graph.RunReadAsync(@"
            MATCH (u:User)-[:TRIGGERED]->(e:Event {eventType:'AUTH_FAILED'})
            RETURN u.userId AS userId, count(e) AS failedCount
            ORDER BY failedCount DESC
        ");

        var answer = rows.Count > 0
            ? "Users linked to failed login events: " + string.Join(", ", rows.Select(r => $"{r["userId"]} ({r["failedCount"]} attempts)"))
            : "No failed login events are currently recorded in the graph.";

        return new
        {
            question,
            answer,
            retrievedNodes = rows.Select(r => new { type = "User", userId = r["userId"].As<string>(), failedLogins = r["failedCount"].As<long>() }),
            retrievedRelationships = new[] { "User-TRIGGERED->Event" },
            retrievalMethod = "cypher-graph-traversal (template-based generation)"
        };
    }
}