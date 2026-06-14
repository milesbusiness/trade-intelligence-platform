using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using TradeIntelligence.Api.Models;

namespace TradeIntelligence.Api.Services;

public interface IComplianceCheckService
{
    Task<ComplianceCheckResult> CheckAsync(ComplianceCheckRequest request, CancellationToken ct = default);
}

public class ComplianceCheckService : IComplianceCheckService
{
    private readonly Kernel _kernel;
    private readonly ILogger<ComplianceCheckService> _logger;

    public ComplianceCheckService(Kernel kernel, ILogger<ComplianceCheckService> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    public async Task<ComplianceCheckResult> CheckAsync(ComplianceCheckRequest request, CancellationToken ct = default)
    {
        var regulations = request.Regulations?.Any() == true
            ? string.Join(", ", request.Regulations)
            : "MiFID II, EMIR";

        var prompt = $"""
            You are a trade compliance expert specialising in EU financial regulation ({regulations}).

            Analyse the following trade document text and:
            1. Identify any compliance issues or potential violations
            2. Flag missing mandatory fields (e.g., LEI, trade venue, timestamps)
            3. Check best execution requirements
            4. Identify EMIR reporting obligations if applicable
            5. Provide a compliance score (0-100, where 100 = fully compliant)

            For each finding, specify:
            - Severity: HIGH / MEDIUM / LOW
            - Regulation article reference
            - Specific issue
            - Recommended action

            DOCUMENT TEXT:
            {request.DocumentText}

            Respond in JSON format:
            {{
              "score": <number>,
              "status": "COMPLIANT" | "REVIEW_REQUIRED" | "NON_COMPLIANT",
              "findings": [
                {{"severity": "HIGH", "article": "MiFID II Art. 26", "issue": "...", "action": "..."}}
              ],
              "summary": "..."
            }}
            """;

        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddUserMessage(prompt);
        var response = await chat.GetChatMessageContentAsync(history, cancellationToken: ct);

        try
        {
            var result = System.Text.Json.JsonSerializer.Deserialize<ComplianceCheckResult>(
                response.Content ?? "{}",
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new ComplianceCheckResult { Score = 0, Status = "ERROR" };

            _logger.LogInformation("Compliance check completed: score={Score}, status={Status}",
                result.Score, result.Status);
            return result;
        }
        catch
        {
            return new ComplianceCheckResult
            {
                Score = 0,
                Status = "ERROR",
                Summary = "Failed to parse compliance check result",
                Findings = []
            };
        }
    }
}
