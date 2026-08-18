using System.Text.Json;
using MongoDB.Driver;
using ParcelFlow.Domain.Entities;

namespace ParcelFlow.Storage.Mongo;

/// <summary>
/// Imports the repo's seed/ JSON data into MongoDB collections on startup.
/// Layout:
///   seed/tenants.json
///   seed/&lt;tenantId&gt;/drivers.json | parcels.json | tasks.json | shifts.json
/// Upserts every document by Id, so re-running against an already-seeded
/// database is safe.
/// </summary>
public static class MongoSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task SeedAsync(IMongoDatabase database, string seedDirectory, CancellationToken ct = default)
    {
        var tenantsFile = Path.Combine(seedDirectory, "tenants.json");
        if (!File.Exists(tenantsFile))
        {
            throw new FileNotFoundException(
                $"Seed file not found: {tenantsFile}. Run the API from the repo so the seed/ directory can be located.");
        }

        var tenants = Deserialize<Tenant>(tenantsFile);
        await UpsertAllAsync(database.GetCollection<Tenant>(MongoCollectionNames.Tenants), tenants, t => t.Id, ct);

        foreach (var tenant in tenants)
        {
            var tenantDir = Path.Combine(seedDirectory, tenant.Id);
            if (!Directory.Exists(tenantDir))
            {
                continue;
            }

            await LoadIntoAsync<Driver>(database, MongoCollectionNames.Drivers, Path.Combine(tenantDir, "drivers.json"), ct);
            await LoadIntoAsync<Parcel>(database, MongoCollectionNames.Parcels, Path.Combine(tenantDir, "parcels.json"), ct);
            await LoadIntoAsync<DeliveryTask>(database, MongoCollectionNames.Tasks, Path.Combine(tenantDir, "tasks.json"), ct);
            await LoadIntoAsync<Shift>(database, MongoCollectionNames.Shifts, Path.Combine(tenantDir, "shifts.json"), ct);
        }
    }

    /// <summary>
    /// Walks up from the application base directory to find the repo's seed/ folder,
    /// so `dotnet run` works from any project directory inside the repo.
    /// </summary>
    public static string? LocateSeedDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "seed");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "tenants.json")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static async Task LoadIntoAsync<T>(IMongoDatabase database, string collectionName, string file, CancellationToken ct)
        where T : TenantDocument
    {
        if (!File.Exists(file))
        {
            return;
        }

        var documents = Deserialize<T>(file);
        await UpsertAllAsync(database.GetCollection<T>(collectionName), documents, d => d.Id, ct);
    }

    private static async Task UpsertAllAsync<T>(
        IMongoCollection<T> collection,
        IReadOnlyList<T> documents,
        Func<T, string> idSelector,
        CancellationToken ct)
    {
        foreach (var document in documents)
        {
            var filter = Builders<T>.Filter.Eq("_id", idSelector(document));
            await collection.ReplaceOneAsync(filter, document, new ReplaceOptions { IsUpsert = true }, ct);
        }
    }

    private static List<T> Deserialize<T>(string file)
    {
        var json = File.ReadAllText(file);
        return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new List<T>();
    }
}
