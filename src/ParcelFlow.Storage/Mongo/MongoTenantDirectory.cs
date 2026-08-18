using MongoDB.Driver;
using ParcelFlow.Domain.Entities;

namespace ParcelFlow.Storage.Mongo;

/// <summary>
/// Platform-level tenant registry backed by the Mongo `tenants` collection.
/// See docs/adr/0002-tenant-isolation-by-tenantid.md — Tenant documents are
/// the one collection not scoped by TenantId.
/// </summary>
public sealed class MongoTenantDirectory : ITenantDirectory
{
    private readonly IMongoCollection<Tenant> _collection;

    public MongoTenantDirectory(IMongoDatabase database)
    {
        _collection = database.GetCollection<Tenant>(MongoCollectionNames.Tenants);
    }

    public async Task<Tenant?> GetAsync(string tenantId, CancellationToken ct = default)
    {
        var filter = Builders<Tenant>.Filter.Eq(t => t.Id, tenantId);
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Tenant>> GetAllActiveAsync(CancellationToken ct = default)
    {
        var filter = Builders<Tenant>.Filter.Eq(t => t.IsActive, true);
        return await _collection.Find(filter).ToListAsync(ct);
    }
}
