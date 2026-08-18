using ParcelFlow.Domain.StateMachine;

namespace ParcelFlow.Domain.Entities;

/// <summary>
/// The unit of work in ParcelFlow: deliver one parcel to its recipient.
/// Lifecycle is governed strictly by <see cref="DeliveryTaskStateMachine"/> —
/// never set <see cref="Status"/> directly outside of it.
/// </summary>
public sealed class DeliveryTask : TenantDocument
{
    public string ParcelId { get; set; } = string.Empty;

    /// <summary>Assigned driver; null until assignment.</summary>
    public string? DriverId { get; set; }

    public DeliveryTaskStatus Status { get; set; } = DeliveryTaskStatus.Created;

    /// <summary>Number of failed delivery attempts so far.</summary>
    public int AttemptCount { get; set; }

    public DateTime? AssignedUtc { get; set; }
    public DateTime? PickedUpUtc { get; set; }
    public DateTime? DeliveredUtc { get; set; }

    /// <summary>Free-text note captured at proof of delivery.</summary>
    public string? PodNote { get; set; }

    /// <summary>Audit trail of every status change.</summary>
    public List<StatusChange> History { get; set; } = new();
}

public sealed class StatusChange
{
    public DeliveryTaskStatus From { get; set; }
    public DeliveryTaskStatus To { get; set; }
    public DateTime AtUtc { get; set; }
    public string? Reason { get; set; }
}
