using System.Text;
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

    /// <summary>
    /// Weekly per-driver performance (tasks delivered, failed attempts,
    /// average hours from assignment to delivery) for the 7 days ending at
    /// <c>asOf</c> (defaults to now), as CSV. Intended for the Monday ops
    /// run - see SOLUTION.md for how this would be scheduled in production.
    /// Example: GET /api/reports/weekly-driver-performance?asOf=2026-07-01
    /// </summary>
    [HttpGet("weekly-driver-performance")]
    public async Task<IActionResult> WeeklyDriverPerformance([FromQuery] DateTime? asOf, CancellationToken ct)
    {
        var rows = await _reports.GetWeeklyDriverPerformanceAsync(asOf ?? DateTime.UtcNow, ct);
        var csv = WeeklyDriverPerformanceCsv.Write(rows);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", "weekly-driver-performance.csv");
    }
}
