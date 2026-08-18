using System.Linq.Expressions;
using MongoDB.Driver;
using ParcelFlow.Domain.Entities;

namespace ParcelFlow.Storage.Mongo;

/// <summary>
/// MongoDB-backed repository (see docker-compose.yml for a local instance).
/// The tenant filter is combined server-side with every query.
/// </summary>
public sealed class MongoTenantScopedRepository<T> : ITenantScopedRepository<T> where T : TenantDocument
{
    private readonly IMongoCollection<T> _collection;

    public MongoTenantScopedRepository(IMongoDatabase database, string collectionName)
    {
        _collection = database.GetCollection<T>(collectionName);
    }

    public async Task<T?> GetAsync(string tenantId, string id, CancellationToken ct = default)
    {
        var filter = Builders<T>.Filter.And(
            Builders<T>.Filter.Eq(d => d.TenantId, tenantId),
            Builders<T>.Filter.Eq(d => d.Id, id));

        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<T>> QueryAsync(string tenantId, Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        var filter = Builders<T>.Filter.And(
            Builders<T>.Filter.Eq(d => d.TenantId, tenantId),
            Builders<T>.Filter.Where(predicate));

        return await _collection.Find(filter).ToListAsync(ct);
    }

    public async Task UpsertAsync(T document, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(document.TenantId))
        {
            throw new InvalidOperationException(
                $"Refusing to persist {typeof(T).Name} '{document.Id}' without a TenantId.");
        }

        var filter = Builders<T>.Filter.Eq(d => d.Id, document.Id);
        await _collection.ReplaceOneAsync(filter, document, new ReplaceOptions { IsUpsert = true }, ct);
    }

    public async Task DeleteAsync(string tenantId, string id, CancellationToken ct = default)
    {
        var filter = Builders<T>.Filter.And(
            Builders<T>.Filter.Eq(d => d.TenantId, tenantId),
            Builders<T>.Filter.Eq(d => d.Id, id));

        await _collection.DeleteOneAsync(filter, ct);
    }

    public async Task<IReadOnlyList<T>> QueryAllTenantsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        return await _collection.Find(Builders<T>.Filter.Where(predicate)).ToListAsync(ct);
    }
}
