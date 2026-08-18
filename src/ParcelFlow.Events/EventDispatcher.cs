using Microsoft.Extensions.Logging;
using ParcelFlow.Domain.Events;

namespace ParcelFlow.Events;

/// <summary>
/// In-process event dispatcher. Evaluates every registered <see cref="IEventRule"/>
/// against the event; rule failures are logged and swallowed so a broken
/// notification can never fail the business operation that raised the event.
///
/// (In production this would sit behind a message bus; in this codebase it is
/// synchronous and in-process by design — see docs/ARCHITECTURE.md §5.)
/// </summary>
public sealed class EventDispatcher : IEventDispatcher
{
    private readonly IEnumerable<IEventRule> _rules;
    private readonly ILogger<EventDispatcher> _logger;

    public EventDispatcher(IEnumerable<IEventRule> rules, ILogger<EventDispatcher> logger)
    {
        _rules = rules;
        _logger = logger;
    }

    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        foreach (var rule in _rules)
        {
            if (!rule.AppliesTo(domainEvent))
            {
                continue;
            }

            try
            {
                await rule.ExecuteAsync(domainEvent, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Event rule {RuleName} failed for event {EventType} (tenant {TenantId})",
                    rule.Name, domainEvent.GetType().Name, domainEvent.TenantId);
            }
        }
    }
}
