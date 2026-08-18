using Microsoft.Extensions.Logging;

namespace ParcelFlow.Events.Actions;

/// <summary>Stub SMS channel — logs instead of sending.</summary>
public sealed class SmsNotificationAction : INotificationAction
{
    private readonly ILogger<SmsNotificationAction> _logger;

    public SmsNotificationAction(ILogger<SmsNotificationAction> logger)
    {
        _logger = logger;
    }

    public string Channel => "sms";

    public Task SendAsync(string tenantId, string recipient, string message, CancellationToken ct)
    {
        _logger.LogInformation("[SMS/{TenantId}] to={Recipient} :: {Message}", tenantId, recipient, message);
        return Task.CompletedTask;
    }
}
