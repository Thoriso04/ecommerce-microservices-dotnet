using Microsoft.AspNetCore.Mvc;
using SocEventService.Data;
using SocEventService.Models;

namespace SocEventService.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly SocDbContext _db;
    private readonly ILogger<EventsController> _logger;
    public EventsController(SocDbContext db, ILogger<EventsController> logger) { _db = db; _logger = logger; }

    [HttpPost]
    public async Task<IActionResult> Ingest(IngestEventRequest req)
    {
        var ev = new SecurityEvent
        {
            EventType = req.EventType,
            Severity = req.Severity,
            ServiceName = req.ServiceName,
            UserId = req.UserId,
            SourceIp = req.SourceIp,
            Endpoint = req.Endpoint,
            HttpMethod = req.HttpMethod,
            StatusCode = req.StatusCode,
            Message = req.Message,
            CorrelationId = req.CorrelationId,
            AffectedEntity = req.AffectedEntity
        };
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();
        return Created($"/api/events/{ev.Id}", ev);
    }

    [HttpGet]
    public async Task<IActionResult> GetRecent([FromQuery] int limit = 100)
    {
        var events = _db.Events.OrderByDescending(e => e.Timestamp).Take(limit).ToList();
        return Ok(events);
    }
}