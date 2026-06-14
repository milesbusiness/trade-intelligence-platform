using Microsoft.AspNetCore.Mvc;
using TradeIntelligence.Api.Models;
using TradeIntelligence.Api.Services;

namespace TradeIntelligence.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ComplianceController : ControllerBase
{
    private readonly IComplianceCheckService _complianceService;

    public ComplianceController(IComplianceCheckService complianceService) => _complianceService = complianceService;

    [HttpPost("check")]
    [ProducesResponseType(typeof(ComplianceCheckResult), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Check([FromBody] ComplianceCheckRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DocumentText))
            return BadRequest(new { error = "documentText is required" });

        var result = await _complianceService.CheckAsync(request, ct);
        return Ok(result);
    }
}
