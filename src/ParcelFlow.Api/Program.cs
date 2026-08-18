using LegacyCourier.Common;
using MongoDB.Driver;
using ParcelFlow.Api.Middleware;
using ParcelFlow.Domain.Entities;
using ParcelFlow.Events;
using ParcelFlow.Events.Actions;
using ParcelFlow.Events.Rules;
using ParcelFlow.Services;
using ParcelFlow.Services.Reporting;
using ParcelFlow.Storage;
using ParcelFlow.Storage.Mongo;
using ParcelFlow.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---- Time ----
builder.Services.AddSingleton<IClock, SystemClock>();

// ---- Storage ----
// MongoDB-backed (see docker-compose.yml for a local instance). The repo's
// seed/ data is imported into the Mongo collections on startup unless
// Storage:Mongo:SeedOnStartup is set to false.
var connectionString = builder.Configuration["Storage:Mongo:ConnectionString"]
                       ?? "mongodb://localhost:27017";
var databaseName = builder.Configuration["Storage:Mongo:Database"] ?? "parcelflow";

var client = new MongoClient(connectionString);
var database = client.GetDatabase(databaseName);

builder.Services.AddSingleton(database);
builder.Services.AddSingleton<ITenantScopedRepository<Driver>>(
    _ => new MongoTenantScopedRepository<Driver>(database, MongoCollectionNames.Drivers));
builder.Services.AddSingleton<ITenantScopedRepository<Parcel>>(
    _ => new MongoTenantScopedRepository<Parcel>(database, MongoCollectionNames.Parcels));
builder.Services.AddSingleton<ITenantScopedRepository<DeliveryTask>>(
    _ => new MongoTenantScopedRepository<DeliveryTask>(database, MongoCollectionNames.Tasks));
builder.Services.AddSingleton<ITenantScopedRepository<Shift>>(
    _ => new MongoTenantScopedRepository<Shift>(database, MongoCollectionNames.Shifts));
builder.Services.AddSingleton<ITenantDirectory>(_ => new MongoTenantDirectory(database));

if (builder.Configuration.GetValue("Storage:Mongo:SeedOnStartup", true))
{
    var seedDir = MongoSeeder.LocateSeedDirectory()
                  ?? throw new InvalidOperationException(
                      "Could not locate the seed/ directory. Run from within the repo, e.g. `dotnet run --project src/ParcelFlow.Api`.");
    await MongoSeeder.SeedAsync(database, seedDir);
}

// ---- Tenant context (scoped: one per request / per worker pass) ----
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

// ---- Domain services ----
builder.Services.AddScoped<ParcelService>();
builder.Services.AddScoped<DeliveryTaskService>();
builder.Services.AddScoped<AssignmentService>();
builder.Services.AddScoped<ShiftService>();
builder.Services.AddScoped<ReportService>();

// ---- Events: dispatcher, rules, actions ----
builder.Services.AddScoped<IEventDispatcher, EventDispatcher>();
builder.Services.AddScoped<EmailNotificationAction>();
builder.Services.AddScoped<SmsNotificationAction>();
builder.Services.AddScoped<OpsWebhookAction>();
builder.Services.AddScoped<IEventRule, RecipientDeliveredNotificationRule>();
builder.Services.AddScoped<IEventRule, RepeatedFailureOpsAlertRule>();

// ---- Background workers ----
builder.Services.AddHostedService<PendingAssignmentsWorker>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseMiddleware<TenantResolutionMiddleware>();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
