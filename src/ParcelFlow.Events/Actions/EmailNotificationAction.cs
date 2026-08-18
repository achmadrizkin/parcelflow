using Microsoft.Extensions.Logging;

namespace ParcelFlow.Events.Actions;

/// <summary>Stub email channel — logs instead of sending. Real impl would call the provider API.</summary>
public sealed class EmailNotificationAction : INotificationAction
{
    private readonly ILogger<EmailNotificationAction> _logger;

    public EmailNotificationAction(ILogger<EmailNotificationAction> logger)
    {
        _logger = logger;
    }

    public string Channel => "email";

    public Task SendAsync(string tenantId, string recipient, string message, CancellationToken ct)
    {
        _logger.LogInformation("[EMAIL/{TenantId}] to={Recipient} :: {Message}", tenantId, recipient, message);
        return Task.CompletedTask;
    }
}
