using Microsoft.Extensions.Logging;

namespace ParcelFlow.Events.Actions;

/// <summary>
/// Stub webhook to the tenant's ops channel (Slack/Teams in production).
/// Logs instead of posting.
/// </summary>
public sealed class OpsWebhookAction : INotificationAction
{
    private readonly ILogger<OpsWebhookAction> _logger;

    public OpsWebhookAction(ILogger<OpsWebhookAction> logger)
    {
        _logger = logger;
    }

    public string Channel => "ops-webhook";

    public Task SendAsync(string tenantId, string recipient, string message, CancellationToken ct)
    {
        _logger.LogWarning("[OPS-WEBHOOK/{TenantId}] channel={Recipient} :: {Message}", tenantId, recipient, message);
        return Task.CompletedTask;
    }
}
