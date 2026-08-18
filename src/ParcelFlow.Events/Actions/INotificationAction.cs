namespace ParcelFlow.Events.Actions;

/// <summary>
/// Outbound notification channel. Implementations in this repo are stubs that
/// log instead of calling real providers — the integration point is what
/// matters, not the provider wiring.
/// </summary>
public interface INotificationAction
{
    string Channel { get; }

    Task SendAsync(string tenantId, string recipient, string message, CancellationToken ct);
}
