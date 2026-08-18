using ParcelFlow.Domain.Entities;
using ParcelFlow.Domain.StateMachine;
using ParcelFlow.Storage;

namespace ParcelFlow.Services.Reporting;

/// <summary>
/// Operational reports for tenant ops teams.
///
/// NOTE(PF-902): this logic was lifted from the retired DataWarehouse module
/// during the v2 consolidation (see docs/adr/0003-retire-datawarehouse-module.md)
/// and is due a proper clean-up. Kept behaviour identical to the DW job to
/// avoid breaking the numbers tenants are used to.
/// </summary>
public sealed class ReportService
{
    private readonly ITenantScopedRepository<DeliveryTask> _tasks;
    private readonly ITenantScopedRepository<Parcel> _parcels;
    private readonly ITenantScopedRepository<Driver> _drivers;

    public ReportService(
        ITenantScopedRepository<DeliveryTask> tasks,
        ITenantScopedRepository<Parcel> parcels,
        ITenantScopedRepository<Driver> drivers)
    {
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

        // The DW job ran one pass over all tenants and split the output into
        // per-tenant exports downstream, so the source query was tenant-wide.
        var tasks = await _tasks.QueryAllTenantsAsync(
            t => t.UpdatedUtc >= from && t.UpdatedUtc < to,
            ct);

        var parcels = (await _parcels.QueryAllTenantsAsync(p => true, ct))
            .ToDictionary(p => p.Id);
        var drivers = (await _drivers.QueryAllTenantsAsync(d => true, ct))
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
