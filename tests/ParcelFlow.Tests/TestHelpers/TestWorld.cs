using LegacyCourier.Common;
using MongoDB.Driver;
using ParcelFlow.Domain.Entities;
using ParcelFlow.Domain.Events;
using ParcelFlow.Events;
using ParcelFlow.Services;
using ParcelFlow.Services.Reporting;
using ParcelFlow.Storage;
using ParcelFlow.Storage.Mongo;

namespace ParcelFlow.Tests.TestHelpers;

/// <summary>
/// A fully wired world for service tests: repositories, tenant context,
/// clock, and a recording event dispatcher. Each instance gets its own
/// throwaway MongoDB database (dropped on Dispose) so tests never see each
/// other's data even when they share a tenant id. Requires a local MongoDB
/// (see docker-compose.yml); override with PARCELFLOW_TEST_MONGO_URI.
/// </summary>
public sealed class TestWorld : IDisposable
{
    private readonly IMongoClient _client;
    private readonly string _databaseName;

    public TestWorld(string tenantId = "test-tenant")
    {
        Clock = new FixedClock(new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc));

        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        TenantContext = tenantContext;

        var connectionString = Environment.GetEnvironmentVariable("PARCELFLOW_TEST_MONGO_URI")
                                ?? "mongodb://localhost:27017";
        _client = new MongoClient(connectionString);
        _databaseName = $"parcelflow_test_{Guid.NewGuid():N}";
        var database = _client.GetDatabase(_databaseName);

        Drivers = new MongoTenantScopedRepository<Driver>(database, MongoCollectionNames.Drivers);
        Parcels = new MongoTenantScopedRepository<Parcel>(database, MongoCollectionNames.Parcels);
        Tasks = new MongoTenantScopedRepository<DeliveryTask>(database, MongoCollectionNames.Tasks);
        Shifts = new MongoTenantScopedRepository<Shift>(database, MongoCollectionNames.Shifts);

        Events = new RecordingEventDispatcher();

        TaskService = new DeliveryTaskService(TenantContext, Tasks, Parcels, Drivers, Shifts, Events, Clock);
        AssignmentService = new AssignmentService(TenantContext, Drivers, Shifts, Tasks, TaskService);
        ShiftService = new ShiftService(TenantContext, Shifts, Drivers, Clock);
        ParcelService = new ParcelService(TenantContext, Parcels, Clock);
        ReportService = new ReportService(Tasks, Parcels, Drivers);
    }

    public FixedClock Clock { get; }
    public TenantContext TenantContext { get; }
    public ITenantScopedRepository<Driver> Drivers { get; }
    public ITenantScopedRepository<Parcel> Parcels { get; }
    public ITenantScopedRepository<DeliveryTask> Tasks { get; }
    public ITenantScopedRepository<Shift> Shifts { get; }
    public RecordingEventDispatcher Events { get; }

    public DeliveryTaskService TaskService { get; }
    public AssignmentService AssignmentService { get; }
    public ShiftService ShiftService { get; }
    public ParcelService ParcelService { get; }
    public ReportService ReportService { get; }

    public string TenantId => TenantContext.TenantId;

    public async Task<Parcel> SeedParcelAsync(string? tenantId = null, string reference = "REF-001", string city = "Jakarta")
    {
        var parcel = new Parcel
        {
            Id = IdGenerator.NewId("parcel"),
            TenantId = tenantId ?? TenantId,
            Reference = reference,
            RecipientName = "Test Recipient",
            RecipientPhone = "+62-000-0000",
            AddressLine = "Jl. Test 1",
            City = city,
            WeightKg = 1.0m,
            CreatedUtc = Clock.UtcNow,
            UpdatedUtc = Clock.UtcNow
        };
        await Parcels.UpsertAsync(parcel);
        return parcel;
    }

    public async Task<Driver> SeedDriverAsync(string? tenantId = null, string name = "Test Driver", int capacity = 10)
    {
        var driver = new Driver
        {
            Id = IdGenerator.NewId("driver"),
            TenantId = tenantId ?? TenantId,
            Name = name,
            Capacity = capacity,
            CreatedUtc = Clock.UtcNow,
            UpdatedUtc = Clock.UtcNow
        };
        await Drivers.UpsertAsync(driver);
        return driver;
    }

    public async Task<Shift> SeedOpenShiftAsync(Driver driver)
    {
        var shift = new Shift
        {
            Id = IdGenerator.NewId("shift"),
            TenantId = driver.TenantId,
            DriverId = driver.Id,
            StartedUtc = Clock.UtcNow.AddHours(-1),
            CreatedUtc = Clock.UtcNow.AddHours(-1),
            UpdatedUtc = Clock.UtcNow.AddHours(-1)
        };
        await Shifts.UpsertAsync(shift);
        return shift;
    }

    public void Dispose()
    {
        _client.DropDatabase(_databaseName);
    }
}

/// <summary>Captures dispatched events for assertions.</summary>
public sealed class RecordingEventDispatcher : IEventDispatcher
{
    public List<IDomainEvent> Dispatched { get; } = new();

    public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        Dispatched.Add(domainEvent);
        return Task.CompletedTask;
    }
}
