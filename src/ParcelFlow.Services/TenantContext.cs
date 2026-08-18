namespace ParcelFlow.Services;

/// <summary>
/// Carries the tenant of the current unit of work (an API request or one
/// worker iteration). Populated by the API's TenantResolutionMiddleware or
/// set explicitly by workers. Services must take their tenant from here —
/// never from user input.
/// </summary>
public interface ITenantContext
{
    string TenantId { get; }
    bool HasTenant { get; }
}

public sealed class TenantContext : ITenantContext
{
    private string? _tenantId;

    public string TenantId => _tenantId
        ?? throw new InvalidOperationException("No tenant set on the current context.");

    public bool HasTenant => _tenantId is not null;

    public void SetTenant(string tenantId)
    {
        _tenantId = tenantId;
    }
}
