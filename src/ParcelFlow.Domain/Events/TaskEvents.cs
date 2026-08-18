using ParcelFlow.Domain.Entities;

namespace ParcelFlow.Domain.Events;

public sealed class TaskAssignedEvent : IDomainEvent
{
    public required string TenantId { get; init; }
    public required DateTime OccurredUtc { get; init; }
    public required DeliveryTask Task { get; init; }
    public required string DriverId { get; init; }
}

public sealed class TaskDeliveredEvent : IDomainEvent
{
    public required string TenantId { get; init; }
    public required DateTime OccurredUtc { get; init; }
    public required DeliveryTask Task { get; init; }
}

public sealed class DeliveryAttemptFailedEvent : IDomainEvent
{
    public required string TenantId { get; init; }
    public required DateTime OccurredUtc { get; init; }
    public required DeliveryTask Task { get; init; }

    /// <summary>1-based attempt number that just failed.</summary>
    public required int AttemptNumber { get; init; }

    public required string Reason { get; init; }
}

public sealed class TaskCancelledEvent : IDomainEvent
{
    public required string TenantId { get; init; }
    public required DateTime OccurredUtc { get; init; }
    public required DeliveryTask Task { get; init; }
    public required string Reason { get; init; }
}

/// <summary>Raised when a driver completes a scheduled return at the hub.</summary>
public sealed class TaskReturnCompletedEvent : IDomainEvent
{
    public required string TenantId { get; init; }
    public required DateTime OccurredUtc { get; init; }
    public required DeliveryTask Task { get; init; }
}
