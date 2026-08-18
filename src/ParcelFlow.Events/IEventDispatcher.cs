using ParcelFlow.Domain.Events;

namespace ParcelFlow.Events;

public interface IEventDispatcher
{
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct = default);
}
