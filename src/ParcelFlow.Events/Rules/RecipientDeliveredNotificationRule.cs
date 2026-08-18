using ParcelFlow.Domain.Entities;
using ParcelFlow.Domain.Events;
using ParcelFlow.Events.Actions;
using ParcelFlow.Storage;

namespace ParcelFlow.Events.Rules;

/// <summary>
/// When a parcel is delivered, notify the recipient by SMS.
/// </summary>
public sealed class RecipientDeliveredNotificationRule : IEventRule
{
    private readonly ITenantScopedRepository<Parcel> _parcels;
    private readonly SmsNotificationAction _sms;

    public RecipientDeliveredNotificationRule(ITenantScopedRepository<Parcel> parcels, SmsNotificationAction sms)
    {
        _parcels = parcels;
        _sms = sms;
    }

    public string Name => "recipient-delivered-notification";

    public bool AppliesTo(IDomainEvent domainEvent) => domainEvent is TaskDeliveredEvent;

    public async Task ExecuteAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        var evt = (TaskDeliveredEvent)domainEvent;

        var parcel = await _parcels.GetAsync(evt.TenantId, evt.Task.ParcelId, ct);
        if (parcel is null)
        {
            return;
        }

        await _sms.SendAsync(
            evt.TenantId,
            parcel.RecipientPhone,
            $"Your parcel {parcel.Reference} has been delivered. Thank you for using our service.",
            ct);
    }
}
