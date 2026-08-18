using ParcelFlow.Domain.Events;

namespace ParcelFlow.Events;

/// <summary>
/// A rule reacts to domain events, typically by triggering one or more
/// notification actions. Rules are registered in DI and evaluated by
/// <see cref="EventDispatcher"/> for every dispatched event.
///
/// Rules must be idempotent-ish and must never throw for business reasons —
/// a failing rule is logged and skipped so it cannot break the operation
/// that raised the event.
/// </summary>
public interface IEventRule
{
    string Name { get; }

    bool AppliesTo(IDomainEvent domainEvent);

    Task ExecuteAsync(IDomainEvent domainEvent, CancellationToken ct);
}
