using ParcelFlow.Domain.Entities;
using ParcelFlow.Domain.Events;
using ParcelFlow.Domain.StateMachine;
using ParcelFlow.Events.Actions;
using ParcelFlow.Storage;

namespace ParcelFlow.Events.Rules;

/// <summary>
/// From the 3rd failed delivery attempt, stop retrying: automatically
/// schedule the task for return, notify the recipient by SMS, and alert the
/// tenant's ops channel.
///
/// This rule performs the state transition itself (rather than delegating
/// to a task service) because ParcelFlow.Events has no reference to
/// ParcelFlow.Services - it operates directly on the repository through
/// <see cref="DeliveryTaskStateMachine"/>, the platform's single source of
/// truth for status changes, same as every other mutation site.
///
/// It does not raise a further domain event for the scheduling itself:
/// this rule is invoked BY <see cref="IEventDispatcher.DispatchAsync"/>, so
/// taking an <see cref="IEventDispatcher"/> dependency here to publish one
/// would make every <see cref="IEventRule"/> registration circular at DI
/// construction time (EventDispatcher needs all rules; this rule would need
/// EventDispatcher). The SMS + ops-webhook side effects required by the spec
/// are sent directly instead.
/// </summary>
public sealed class ScheduleReturnOnThirdFailedAttemptRule : IEventRule
{
    private const int ReturnAfterAttempt = 3;

    private readonly ITenantScopedRepository<DeliveryTask> _tasks;
    private readonly ITenantScopedRepository<Parcel> _parcels;
    private readonly SmsNotificationAction _sms;
    private readonly OpsWebhookAction _opsWebhook;

    public ScheduleReturnOnThirdFailedAttemptRule(
        ITenantScopedRepository<DeliveryTask> tasks,
        ITenantScopedRepository<Parcel> parcels,
        SmsNotificationAction sms,
        OpsWebhookAction opsWebhook)
    {
        _tasks = tasks;
        _parcels = parcels;
        _sms = sms;
        _opsWebhook = opsWebhook;
    }

    public string Name => "schedule-return-on-third-failed-attempt";

    public bool AppliesTo(IDomainEvent domainEvent)
    {
        return domainEvent is DeliveryAttemptFailedEvent { AttemptNumber: >= ReturnAfterAttempt };
    }

    public async Task ExecuteAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        var evt = (DeliveryAttemptFailedEvent)domainEvent;
        var task = evt.Task;

        // The state machine only allows AttemptFailed -> ReturnScheduled, so
        // a task that already went through this once (or was cancelled)
        // cannot be scheduled again - no idempotency flag needed.
        if (!DeliveryTaskStateMachine.CanTransition(task.Status, DeliveryTaskStatus.ReturnScheduled))
        {
            return;
        }

        DeliveryTaskStateMachine.Transition(
            task,
            DeliveryTaskStatus.ReturnScheduled,
            evt.OccurredUtc,
            $"Auto-scheduled for return after {evt.AttemptNumber} failed delivery attempts");
        task.ReturnScheduledUtc = evt.OccurredUtc;
        await _tasks.UpsertAsync(task, ct);

        var parcel = await _parcels.GetAsync(evt.TenantId, task.ParcelId, ct);
        if (parcel is not null)
        {
            await _sms.SendAsync(
                evt.TenantId,
                parcel.RecipientPhone,
                $"We were unable to deliver your parcel {parcel.Reference} after several attempts. " +
                "It is being returned to the sender.",
                ct);
        }

        await _opsWebhook.SendAsync(
            evt.TenantId,
            "ops-alerts",
            $"Task {task.Id} scheduled for return to sender after {evt.AttemptNumber} failed delivery attempts.",
            ct);
    }
}
