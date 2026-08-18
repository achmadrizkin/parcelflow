using System.Linq.Expressions;
using ParcelFlow.Domain.Entities;

namespace ParcelFlow.Storage;

/// <summary>
/// The ONLY sanctioned way to read and write tenant data.
///
/// Every method takes an explicit tenantId and guarantees results are scoped
/// to that tenant. See docs/adr/0002-tenant-isolation-by-tenantid.md — tenant
/// isolation is a security boundary, not a convention.
/// </summary>
public interface ITenantScopedRepository<T> where T : TenantDocument
{
    Task<T?> GetAsync(string tenantId, string id, CancellationToken ct = default);

    Task<IReadOnlyList<T>> QueryAsync(string tenantId, Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    Task UpsertAsync(T document, CancellationToken ct = default);

    Task DeleteAsync(string tenantId, string id, CancellationToken ct = default);

    /// <summary>
    /// LEGACY — queries across ALL tenants. Retained only for the platform
    /// migration tooling that ran during the LegacyCourier import (see
    /// docs/adr/0003-retire-datawarehouse-module.md). Must never be called
    /// from request-handling code paths.
    /// </summary>
    Task<IReadOnlyList<T>> QueryAllTenantsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
}
