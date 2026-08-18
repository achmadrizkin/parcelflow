namespace ParcelFlow.Domain.Events;

/// <summary>
/// Something that happened in the domain that other parts of the platform may
/// react to (notifications, alerts, integrations). Events are dispatched
/// in-process by ParcelFlow.Events; see docs/ARCHITECTURE.md §5.
/// </summary>
public interface IDomainEvent
{
    string TenantId { get; }
    DateTime OccurredUtc { get; }
}
