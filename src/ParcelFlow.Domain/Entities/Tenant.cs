namespace ParcelFlow.Domain.Entities;

/// <summary>
/// A tenant is a carrier company using the ParcelFlow platform.
/// Tenant records themselves live in a platform-level collection and are the
/// only documents not scoped by TenantId.
/// </summary>
public sealed class Tenant
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
