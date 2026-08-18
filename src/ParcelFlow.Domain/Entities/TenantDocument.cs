namespace ParcelFlow.Domain.Entities;

/// <summary>
/// Base class for every persisted document in ParcelFlow.
///
/// ParcelFlow is a multi-tenant SaaS on a SINGLE shared database. There is no
/// physical separation between tenants — <see cref="TenantId"/> on each document
/// is the only isolation boundary. Every read and write must be scoped to a tenant.
/// See docs/adr/0002-tenant-isolation-by-tenantid.md.
/// </summary>
public abstract class TenantDocument
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The tenant (carrier) that owns this document. Never empty.</summary>
    public string TenantId { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
