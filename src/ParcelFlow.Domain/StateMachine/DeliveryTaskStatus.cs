namespace ParcelFlow.Domain.StateMachine;

public enum DeliveryTaskStatus
{
    /// <summary>Task exists but no driver is assigned yet.</summary>
    Created = 0,

    /// <summary>A driver has been assigned but has not collected the parcel.</summary>
    Assigned = 1,

    /// <summary>Driver has collected the parcel from the hub.</summary>
    PickedUp = 2,

    /// <summary>Driver is on the way to the recipient.</summary>
    InTransit = 3,

    /// <summary>The most recent delivery attempt failed (recipient absent, address wrong, ...).</summary>
    AttemptFailed = 4,

    /// <summary>Parcel handed over to recipient. Terminal.</summary>
    Delivered = 5,

    /// <summary>Task cancelled by the operator. Terminal.</summary>
    Cancelled = 6,

    /// <summary>
    /// Delivery abandoned after repeated failed attempts; the parcel is on
    /// its way back to the sender's hub.
    /// </summary>
    ReturnScheduled = 7,

    /// <summary>Parcel handed back to the sender at the hub. Terminal.</summary>
    ReturnCompleted = 8
}
