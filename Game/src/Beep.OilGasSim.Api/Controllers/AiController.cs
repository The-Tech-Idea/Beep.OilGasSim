using Beep.OilGasSim.AI.Context;
using Beep.OilGasSim.AI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Beep.OilGasSim.Api.Controllers;

[ApiController]
[Route("api/game-sessions/{sessionId:guid}/ai")]
public sealed class AiController : ControllerBase
{
    private readonly IAiAdvisorService _advisor;
    private readonly IAiTurnReportService _turnReport;

    public AiController(IAiAdvisorService advisor, IAiTurnReportService turnReport)
    {
        _advisor = advisor;
        _turnReport = turnReport;
    }

    [HttpPost("ask")]
    public async Task<ActionResult<AiAdvisorResponse>> Ask(
        Guid sessionId,
        [FromBody] AiAskRequest body,
        CancellationToken ct)
    {
        var response = await _advisor.AskAsync(sessionId, new AiAdvisorRequest
        {
            CompanyId = body.CompanyId,
            AdvisorType = ParseAdvisor(body.AdvisorType),
            Message = body.Message,
            SelectedBlockId = body.SelectedBlockId,
            SelectedAssetId = body.SelectedAssetId
        }, ct);

        return Ok(response);
    }

    [HttpGet("turn-report")]
    public async Task<ActionResult<AiTurnReport>> GetTurnReport(
        Guid sessionId,
        [FromQuery] Guid companyId,
        CancellationToken ct)
    {
        var report = await _turnReport.GetLatestReportAsync(sessionId, companyId, ct);
        return report is null ? NotFound() : Ok(report);
    }

    private static AiAdvisorType ParseAdvisor(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "geologist" => AiAdvisorType.Geologist,
            "cfo" => AiAdvisorType.Cfo,
            "hse" => AiAdvisorType.Hse,
            _ => AiAdvisorType.Strategy
        };
}

public sealed class AiAskRequest
{
    public Guid CompanyId { get; set; }
    public string AdvisorType { get; set; } = "Strategy";
    public string Message { get; set; } = "";
    public Guid? SelectedBlockId { get; set; }
    public Guid? SelectedAssetId { get; set; }
}
