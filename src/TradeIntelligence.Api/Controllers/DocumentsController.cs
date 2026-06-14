using Microsoft.AspNetCore.Mvc;
using TradeIntelligence.Api.Services;

namespace TradeIntelligence.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentIngestionService _ingestionService;

    public DocumentsController(IDocumentIngestionService ingestionService) => _ingestionService = ingestionService;

    [HttpPost("ingest")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Ingest(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded" });

        try
        {
            var result = await _ingestionService.IngestAsync(file, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var docs = await _ingestionService.ListDocumentsAsync(ct);
        return Ok(docs);
    }

    [HttpDelete("{documentId}")]
    public async Task<IActionResult> Delete(string documentId, CancellationToken ct)
    {
        await _ingestionService.DeleteDocumentAsync(documentId, ct);
        return NoContent();
    }
}
