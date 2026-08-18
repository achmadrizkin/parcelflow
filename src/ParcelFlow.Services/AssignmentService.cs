using LegacyCourier.Common;
using ParcelFlow.Domain.Entities;
using ParcelFlow.Domain.StateMachine;
using ParcelFlow.Storage;

namespace ParcelFlow.Services;

/// <summary>
/// Picks the best available driver for a task. "Best" is currently the driver
/// with the most spare capacity among those on an open shift — the real
/// routing engine is out of scope for this codebase.
/// </summary>
public sealed class AssignmentService
{
    private readonly ITenantContext _tenant;
    private readonly ITenantScopedRepository<Driver> _drivers;
    private readonly ITenantScopedRepository<Shift> _shifts;
    private readonly ITenantScopedRepository<DeliveryTask> _tasks;
    private readonly DeliveryTaskService _taskService;

    public AssignmentService(
        ITenantContext tenant,
        ITenantScopedRepository<Driver> drivers,
        ITenantScopedRepository<Shift> shifts,
        ITenantScopedRepository<DeliveryTask> tasks,
        DeliveryTaskService taskService)
    {
        _tenant = tenant;
        _drivers = drivers;
        _shifts = shifts;
        _tasks = tasks;
        _taskService = taskService;
    }

    /// <summary>
    /// Finds an available driver and assigns the task to them.
    /// Fails (without side effects) when no driver has spare capacity.
    /// </summary>
    public async Task<Result<DeliveryTask>> AutoAssignAsync(string taskId, CancellationToken ct = default)
    {
        var driver = await FindAvailableDriverAsync(ct);
        if (driver is null)
        {
            return Result<DeliveryTask>.Fail("No driver with spare capacity is on shift.");
        }

        return await _taskService.AssignAsync(taskId, driver.Id, ct);
    }

    public async Task<Driver?> FindAvailableDriverAsync(CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId;

        var openShifts = await _shifts.QueryAsync(tenantId, s => s.EndedUtc == null, ct);
        if (openShifts.Count == 0)
        {
            return null;
        }

        var onShiftDriverIds = openShifts.Select(s => s.DriverId).ToHashSet();

        var candidates = await _drivers.QueryAsync(
            tenantId,
            d => d.IsActive && onShiftDriverIds.Contains(d.Id),
            ct);

        Driver? best = null;
        var bestSpare = 0;

        foreach (var driver in candidates)
        {
            var driverId = driver.Id;
            var activeTasks = await _tasks.QueryAsync(
                tenantId,
                t => t.DriverId == driverId
                     && t.Status != DeliveryTaskStatus.Delivered
                     && t.Status != DeliveryTaskStatus.Cancelled,
                ct);

            var spare = driver.Capacity - activeTasks.Count;
            if (spare > bestSpare)
            {
                bestSpare = spare;
                best = driver;
            }
        }

        return best;
    }
}
