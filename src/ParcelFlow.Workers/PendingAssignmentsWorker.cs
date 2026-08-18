using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ParcelFlow.Domain.Entities;
using ParcelFlow.Domain.StateMachine;
using ParcelFlow.Services;
using ParcelFlow.Storage;

namespace ParcelFlow.Workers;

/// <summary>
/// Background worker that sweeps every tenant for unassigned tasks and
/// auto-assigns them to available drivers. Mirrors the production pattern:
/// one worker iterates tenants, creating a fresh scoped tenant context per
/// tenant per pass.
/// </summary>
public sealed class PendingAssignmentsWorker : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingAssignmentsWorker> _logger;

    public PendingAssignmentsWorker(IServiceScopeFactory scopeFactory, ILogger<PendingAssignmentsWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PendingAssignmentsWorker started (interval {Interval})", SweepInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAllTenantsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Assignment sweep failed; will retry next interval");
            }

            try
            {
                await Task.Delay(SweepInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SweepAllTenantsAsync(CancellationToken ct)
    {
        using var rootScope = _scopeFactory.CreateScope();
        var directory = rootScope.ServiceProvider.GetRequiredService<ITenantDirectory>();
        var tenants = await directory.GetAllActiveAsync(ct);

        foreach (var tenant in tenants)
        {
            // Fresh scope per tenant so the scoped TenantContext and services
            // never leak from one tenant's pass into another's.
            using var scope = _scopeFactory.CreateScope();

            var tenantContext = (TenantContext)scope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantContext.SetTenant(tenant.Id);

            var tasks = scope.ServiceProvider.GetRequiredService<ITenantScopedRepository<DeliveryTask>>();
            var assignment = scope.ServiceProvider.GetRequiredService<AssignmentService>();

            var pending = await tasks.QueryAsync(tenant.Id, t => t.Status == DeliveryTaskStatus.Created, ct);

            foreach (var task in pending)
            {
                var result = await assignment.AutoAssignAsync(task.Id, ct);
                if (result.IsSuccess)
                {
                    _logger.LogInformation("[{TenantId}] auto-assigned task {TaskId} to driver {DriverId}",
                        tenant.Id, task.Id, result.Value!.DriverId);
                }
            }
        }
    }
}
