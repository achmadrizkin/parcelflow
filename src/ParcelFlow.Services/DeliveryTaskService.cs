using LegacyCourier.Common;
using ParcelFlow.Domain.Entities;
using ParcelFlow.Domain.Events;
using ParcelFlow.Domain.StateMachine;
using ParcelFlow.Events;
using ParcelFlow.Storage;

namespace ParcelFlow.Services;

/// <summary>
/// All lifecycle operations on delivery tasks. Every status change goes
/// through <see cref="DeliveryTaskStateMachine"/> and emits domain events.
/// </summary>
public sealed class DeliveryTaskService
{
    private readonly ITenantContext _tenant;
    private readonly ITenantScopedRepository<DeliveryTask> _tasks;
    private readonly ITenantScopedRepository<Parcel> _parcels;
    private readonly ITenantScopedRepository<Driver> _drivers;
    private readonly ITenantScopedRepository<Shift> _shifts;
    private readonly IEventDispatcher _events;
    private readonly IClock _clock;

    public DeliveryTaskService(
        ITenantContext tenant,
        ITenantScopedRepository<DeliveryTask> tasks,
        ITenantScopedRepository<Parcel> parcels,
        ITenantScopedRepository<Driver> drivers,
        ITenantScopedRepository<Shift> shifts,
        IEventDispatcher events,
        IClock clock)
    {
        _tenant = tenant;
        _tasks = tasks;
        _parcels = parcels;
        _drivers = drivers;
        _shifts = shifts;
        _events = events;
        _clock = clock;
    }

    public async Task<Result<DeliveryTask>> CreateForParcelAsync(string parcelId, CancellationToken ct = default)
    {
        var parcel = await _parcels.GetAsync(_tenant.TenantId, parcelId, ct);
        if (parcel is null)
        {
            return Result<DeliveryTask>.Fail($"Parcel '{parcelId}' not found.");
        }

        var existing = await _tasks.QueryAsync(
            _tenant.TenantId,
            t => t.ParcelId == parcelId
                 && t.Status != DeliveryTaskStatus.Delivered
                 && t.Status != DeliveryTaskStatus.Cancelled,
            ct);
        if (existing.Count > 0)
        {
            return Result<DeliveryTask>.Fail($"Parcel '{parcelId}' already has an active delivery task.");
        }

        var now = _clock.UtcNow;
        var task = new DeliveryTask
        {
            Id = IdGenerator.NewId("task"),
            TenantId = _tenant.TenantId,
            ParcelId = parcelId,
            Status = DeliveryTaskStatus.Created,
            CreatedUtc = now,
            UpdatedUtc = now
        };

        await _tasks.UpsertAsync(task, ct);
        return Result<DeliveryTask>.Ok(task);
    }

    public async Task<Result<DeliveryTask>> AssignAsync(string taskId, string driverId, CancellationToken ct = default)
    {
        var task = await _tasks.GetAsync(_tenant.TenantId, taskId, ct);
        if (task is null)
        {
            return Result<DeliveryTask>.Fail($"Task '{taskId}' not found.");
        }

        var driver = await _drivers.GetAsync(_tenant.TenantId, driverId, ct);
        if (driver is null || !driver.IsActive)
        {
            return Result<DeliveryTask>.Fail($"Driver '{driverId}' not found or inactive.");
        }

        var openShifts = await _shifts.QueryAsync(
            _tenant.TenantId,
            s => s.DriverId == driverId && s.EndedUtc == null,
            ct);
        if (openShifts.Count == 0)
        {
            return Result<DeliveryTask>.Fail($"Driver '{driverId}' has no open shift.");
        }

        var now = _clock.UtcNow;
        try
        {
            DeliveryTaskStateMachine.Transition(task, DeliveryTaskStatus.Assigned, now, $"Assigned to {driver.Name}");
        }
        catch (InvalidStateTransitionException ex)
        {
            return Result<DeliveryTask>.Fail(ex.Message);
        }

        task.DriverId = driverId;
        task.AssignedUtc = now;
        await _tasks.UpsertAsync(task, ct);

        await _events.DispatchAsync(new TaskAssignedEvent
        {
            TenantId = _tenant.TenantId,
            OccurredUtc = now,
            Task = task,
            DriverId = driverId
        }, ct);

        return Result<DeliveryTask>.Ok(task);
    }

    public async Task<Result<DeliveryTask>> RecordPickupAsync(string taskId, CancellationToken ct = default)
    {
        return await TransitionAsync(taskId, DeliveryTaskStatus.PickedUp, "Parcel collected from hub",
            (task, now) => task.PickedUpUtc = now, ct);
    }

    public async Task<Result<DeliveryTask>> StartTransitAsync(string taskId, CancellationToken ct = default)
    {
        return await TransitionAsync(taskId, DeliveryTaskStatus.InTransit, "En route to recipient", null, ct);
    }

    public async Task<Result<DeliveryTask>> MarkDeliveredAsync(string taskId, string? podNote, CancellationToken ct = default)
    {
        var result = await TransitionAsync(taskId, DeliveryTaskStatus.Delivered, "Handed to recipient",
            (task, now) =>
            {
                task.DeliveredUtc = now;
                task.PodNote = podNote;
            }, ct);

        if (result.IsSuccess && result.Value is not null)
        {
            await _events.DispatchAsync(new TaskDeliveredEvent
            {
                TenantId = _tenant.TenantId,
                OccurredUtc = _clock.UtcNow,
                Task = result.Value
            }, ct);
        }

        return result;
    }

    public async Task<Result<DeliveryTask>> RecordFailedAttemptAsync(string taskId, string reason, CancellationToken ct = default)
    {
        var result = await TransitionAsync(taskId, DeliveryTaskStatus.AttemptFailed, reason,
            (task, _) => task.AttemptCount++, ct);

        if (result.IsSuccess && result.Value is not null)
        {
            await _events.DispatchAsync(new DeliveryAttemptFailedEvent
            {
                TenantId = _tenant.TenantId,
                OccurredUtc = _clock.UtcNow,
                Task = result.Value,
                AttemptNumber = result.Value.AttemptCount,
                Reason = reason
            }, ct);
        }

        return result;
    }

    /// <summary>Puts a failed task back on the road for another attempt.</summary>
    public async Task<Result<DeliveryTask>> RetryAsync(string taskId, CancellationToken ct = default)
    {
        return await TransitionAsync(taskId, DeliveryTaskStatus.InTransit, "Retrying delivery", null, ct);
    }

    /// <summary>Called by the hub when a driver hands back a returned parcel, closing the task.</summary>
    public async Task<Result<DeliveryTask>> CompleteReturnAsync(string taskId, string? note, CancellationToken ct = default)
    {
        var result = await TransitionAsync(taskId, DeliveryTaskStatus.ReturnCompleted, note ?? "Return completed at hub",
            (task, now) => task.ReturnCompletedUtc = now, ct);

        if (result.IsSuccess && result.Value is not null)
        {
            await _events.DispatchAsync(new TaskReturnCompletedEvent
            {
                TenantId = _tenant.TenantId,
                OccurredUtc = _clock.UtcNow,
                Task = result.Value
            }, ct);
        }

        return result;
    }

    public async Task<Result<DeliveryTask>> CancelAsync(string taskId, string reason, CancellationToken ct = default)
    {
        var result = await TransitionAsync(taskId, DeliveryTaskStatus.Cancelled, reason, null, ct);

        if (result.IsSuccess && result.Value is not null)
        {
            await _events.DispatchAsync(new TaskCancelledEvent
            {
                TenantId = _tenant.TenantId,
                OccurredUtc = _clock.UtcNow,
                Task = result.Value,
                Reason = reason
            }, ct);
        }

        return result;
    }

    private async Task<Result<DeliveryTask>> TransitionAsync(
        string taskId,
        DeliveryTaskStatus to,
        string reason,
        Action<DeliveryTask, DateTime>? mutate,
        CancellationToken ct)
    {
        var task = await _tasks.GetAsync(_tenant.TenantId, taskId, ct);
        if (task is null)
        {
            return Result<DeliveryTask>.Fail($"Task '{taskId}' not found.");
        }

        var now = _clock.UtcNow;
        try
        {
            DeliveryTaskStateMachine.Transition(task, to, now, reason);
        }
        catch (InvalidStateTransitionException ex)
        {
            return Result<DeliveryTask>.Fail(ex.Message);
        }

        mutate?.Invoke(task, now);
        await _tasks.UpsertAsync(task, ct);

        return Result<DeliveryTask>.Ok(task);
    }
}
