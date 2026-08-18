using ParcelFlow.Domain.Events;
using ParcelFlow.Events.Actions;

namespace ParcelFlow.Events.Rules;

/// <summary>
/// From the second failed delivery attempt onwards, alert the tenant's ops
/// channel so a human can decide what to do with the parcel.
/// </summary>
public sealed class RepeatedFailureOpsAlertRule : IEventRule
{
    private const int AlertFromAttempt = 2;

    private readonly OpsWebhookAction _opsWebhook;

    public RepeatedFailureOpsAlertRule(OpsWebhookAction opsWebhook)
    {
        _opsWebhook = opsWebhook;
    }

    public string Name => "repeated-failure-ops-alert";

    public bool AppliesTo(IDomainEvent domainEvent)
    {
        return domainEvent is DeliveryAttemptFailedEvent { AttemptNumber: >= AlertFromAttempt };
    }

    public async Task ExecuteAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        var evt = (DeliveryAttemptFailedEvent)domainEvent;

        await _opsWebhook.SendAsync(
            evt.TenantId,
            "ops-alerts",
            $"Task {evt.Task.Id} failed delivery attempt #{evt.AttemptNumber} (reason: {evt.Reason}). Manual review may be needed.",
            ct);
    }
}
