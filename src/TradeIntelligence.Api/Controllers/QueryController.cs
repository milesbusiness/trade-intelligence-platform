using Microsoft.AspNetCore.Mvc;
using TradeIntelligence.Api.Models;
using TradeIntelligence.Api.Services;

namespace TradeIntelligence.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class QueryController : ControllerBase
{
    private readonly IRagQueryService _ragService;

    public QueryController(IRagQueryService ragService) => _ragService = ragService;

    [HttpPost]
    [ProducesResponseType(typeof(QueryResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Query([FromBody] QueryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { error = "question is required" });

        var result = await _ragService.QueryAsync(request, ct);
        return Ok(result);
    }

    [HttpGet("history")]
    [ProducesResponseType(typeof(IReadOnlyList<QueryHistoryItem>), 200)]
    public async Task<IActionResult> GetHistory([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var history = await _ragService.GetHistoryAsync(limit, ct);
        return Ok(history);
    }
}
