using ParcelFlow.Domain.Entities;

namespace ParcelFlow.Storage;

/// <summary>
/// Platform-level registry of tenants. Used by the API middleware to validate
/// the incoming tenant header and by workers to enumerate tenants.
/// </summary>
public interface ITenantDirectory
{
    Task<Tenant?> GetAsync(string tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Tenant>> GetAllActiveAsync(CancellationToken ct = default);
}
