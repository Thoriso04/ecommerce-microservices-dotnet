using Microsoft.AspNetCore.Mvc;
using SocEventService.Services;

namespace SocEventService.Controllers;

[ApiController]
[Route("api/graph")]
public class GraphController : ControllerBase
{
    private readonly GraphService _graph;
    public GraphController(GraphService graph) { _graph = graph; }

    [HttpGet("alerts-by-service/{serviceName}")]
    public async Task<IActionResult> AlertsByService(string serviceName)
    {
        var result = await _graph.RunReadAsync(@"
            MATCH (a:Alert)-[:AFFECTS]->(s:Service {name:$serviceName})
            RETURN a.alertId AS alertId, a.ruleName AS ruleName, a.severity AS severity
        ", new { serviceName });
        return Ok(result.Select(r => new { alertId = r["alertId"], ruleName = r["ruleName"], severity = r["severity"] }));
    }

    [HttpGet("users-with-high-severity-alerts")]
    public async Task<IActionResult> UsersWithHighSeverityAlerts()
    {
        var result = await _graph.RunReadAsync(@"
            MATCH (u:User)-[:TRIGGERED]->(e:Event)-[:CREATED_ALERT]->(a:Alert {severity:'HIGH'})
            RETURN DISTINCT u.userId AS userId, a.alertId AS alertId, a.ruleName AS ruleName
        ");
        return Ok(result.Select(r => new { userId = r["userId"], alertId = r["alertId"], ruleName = r["ruleName"] }));
    }

    [HttpGet("threats-for-service/{serviceName}")]
    public async Task<IActionResult> ThreatsForService(string serviceName)
    {
        var result = await _graph.RunReadAsync(@"
            MATCH (t:Threat)-[:TARGETS]->(s:Service {name:$serviceName})
            RETURN t.name AS threat, t.description AS description
        ", new { serviceName });
        return Ok(result.Select(r => new { threat = r["threat"], description = r["description"] }));
    }

    [HttpGet("controls-for-alert/{alertId}")]
    public async Task<IActionResult> ControlsForAlert(string alertId)
    {
        var result = await _graph.RunReadAsync(@"
            MATCH (a:Alert {alertId:$alertId})-[:INDICATES]->(t:Threat)<-[:MITIGATES]-(c:Control)
            RETURN c.name AS control, c.description AS description
        ", new { alertId });
        return Ok(result.Select(r => new { control = r["control"], description = r["description"] }));
    }

    [HttpGet("dependency-impact/{serviceName}")]
    public async Task<IActionResult> DependencyImpact(string serviceName)
    {
        var result = await _graph.RunReadAsync(@"
            MATCH (dependent:Service)-[:DEPENDS_ON]->(s:Service {name:$serviceName})
            RETURN dependent.name AS dependentService
        ", new { serviceName });
        return Ok(result.Select(r => new { dependentService = r["dependentService"] }));
    }
}