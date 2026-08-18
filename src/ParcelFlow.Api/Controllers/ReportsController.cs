using Microsoft.AspNetCore.Mvc;
using ParcelFlow.Services.Reporting;

namespace ParcelFlow.Api.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly ReportService _reports;

    public ReportsController(ReportService reports)
    {
        _reports = reports;
    }

    /// <summary>
    /// Daily delivery summary for the tenant's ops team.
    /// Example: GET /api/reports/daily-summary?day=2026-07-01
    /// </summary>
    [HttpGet("daily-summary")]
    public async Task<IActionResult> DailySummary([FromQuery] DateTime day, CancellationToken ct)
    {
        if (day == default)
        {
            return BadRequest(new { error = "Provide a day, e.g. ?day=2026-07-01" });
        }

        var report = await _reports.GetDailySummaryAsync(day, ct);
        return Ok(report);
    }
}
