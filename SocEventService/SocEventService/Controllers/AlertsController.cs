using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocEventService.Data;

namespace SocEventService.Controllers;

[ApiController]
[Route("api/alerts")]
public class AlertsController : ControllerBase
{
    private readonly SocDbContext _db;
    public AlertsController(SocDbContext db) { _db = db; }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.Alerts.OrderByDescending(a => a.Timestamp).ToListAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var a = await _db.Alerts.FindAsync(id);
        return a == null ? NotFound() : Ok(a);
    }
}