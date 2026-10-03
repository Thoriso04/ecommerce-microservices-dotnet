using Microsoft.AspNetCore.Mvc;
using SocEventService.Services;

namespace SocEventService.Controllers;

public record RagRequest(string Question);

[ApiController]
[Route("api/rag")]
public class RagController : ControllerBase
{
    private readonly RagService _rag;
    public RagController(RagService rag) { _rag = rag; }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask(RagRequest req)
    {
        var result = await _rag.AnswerAsync(req.Question);
        return Ok(result);
    }
}