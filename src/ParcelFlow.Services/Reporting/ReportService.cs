using ParcelFlow.Domain.Entities;
using ParcelFlow.Domain.StateMachine;
using ParcelFlow.Services;
using ParcelFlow.Storage;

namespace ParcelFlow.Services.Reporting;

/// <summary>
/// Operational reports for tenant ops teams.
///
/// NOTE(PF-902, resolved): this logic was lifted from the retired
/// DataWarehouse module during the v2 consolidation (see
/// docs/adr/0003-retire-datawarehouse-module.md) and ran an unscoped,
/// cross-tenant query left over from the DW's single-pass design — see
/// docs/adr/0005-close-pf902-tenant-scope-reportservice.md for the fix
/// (PF-1287). All reads here are now scoped to the current tenant, matching
/// every other request-handling code path (ADR-0002).
/// </summary>
public sealed class ReportService
{
    private readonly ITenantContext _tenant;
    private readonly ITenantScopedRepository<DeliveryTask> _tasks;
    private readonly ITenantScopedRepository<Parcel> _parcels;
    private readonly ITenantScopedRepository<Driver> _drivers;

    public ReportService(
        ITenantContext tenant,
        ITenantScopedRepository<DeliveryTask> tasks,
        ITenantScopedRepository<Parcel> parcels,
        ITenantScopedRepository<Driver> drivers)
    {
        _tenant = tenant;
        _tasks = tasks;
        _parcels = parcels;
        _drivers = drivers;
    }

    /// <summary>
    /// Daily delivery summary: every task that reached a terminal state or had
    /// a failed attempt on the given UTC day, with totals.
    /// </summary>
    public async Task<DailySummaryReport> GetDailySummaryAsync(DateTime dayUtc, CancellationToken ct = default)
    {
        var from = dayUtc.Date;
        var to = from.AddDays(1);
        var tenantId = _tenant.TenantId;

        var tasks = await _tasks.QueryAsync(
            tenantId,
            t => t.UpdatedUtc >= from && t.UpdatedUtc < to,
            ct);

        var parcels = (await _parcels.QueryAsync(tenantId, p => true, ct))
            .ToDictionary(p => p.Id);
        var drivers = (await _drivers.QueryAsync(tenantId, d => true, ct))
            .ToDictionary(d => d.Id);

        var rows = new List<DailySummaryRow>();
        foreach (var task in tasks)
        {
            parcels.TryGetValue(task.ParcelId, out var parcel);
            Driver? driver = null;
            if (task.DriverId is not null)
            {
                drivers.TryGetValue(task.DriverId, out driver);
            }

            rows.Add(new DailySummaryRow
            {
                TaskId = task.Id,
                ParcelReference = parcel?.Reference ?? "(unknown)",
                RecipientCity = parcel?.City ?? "(unknown)",
                DriverName = driver?.Name ?? "(unassigned)",
                Status = task.Status.ToString(),
                AttemptCount = task.AttemptCount
            });
        }

        return new DailySummaryReport
        {
            DayUtc = from,
            TotalDelivered = tasks.Count(t => t.Status == DeliveryTaskStatus.Delivered),
            TotalFailedAttempts = tasks.Sum(t => t.AttemptCount),
            TotalCancelled = tasks.Count(t => t.Status == DeliveryTaskStatus.Cancelled),
            Rows = rows
        };
    }
}

public sealed class DailySummaryReport
{
    public DateTime DayUtc { get; set; }
    public int TotalDelivered { get; set; }
    public int TotalFailedAttempts { get; set; }
    public int TotalCancelled { get; set; }
    public List<DailySummaryRow> Rows { get; set; } = new();
}

public sealed class DailySummaryRow
{
    public string TaskId { get; set; } = string.Empty;
    public string ParcelReference { get; set; } = string.Empty;
    public string RecipientCity { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
}
