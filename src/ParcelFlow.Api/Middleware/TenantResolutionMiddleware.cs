using ParcelFlow.Services;
using ParcelFlow.Storage;

namespace ParcelFlow.Api.Middleware;

/// <summary>
/// Resolves the tenant for every API request from the X-Tenant-Id header,
/// validates it against the tenant directory, and populates the scoped
/// <see cref="ITenantContext"/>. Requests without a valid tenant are rejected
/// before they reach any controller.
///
/// (Production uses API keys / JWT claims to establish the tenant; the plain
/// header keeps this codebase easy to exercise with curl.)
/// </summary>
public sealed class TenantResolutionMiddleware
{
    public const string TenantHeader = "X-Tenant-Id";

    private static readonly string[] ExemptPathPrefixes = { "/health", "/swagger" };

    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, ITenantDirectory tenantDirectory)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (ExemptPathPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var tenantId = context.Request.Headers[TenantHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = $"Missing {TenantHeader} header." });
            return;
        }

        var tenant = await tenantDirectory.GetAsync(tenantId, context.RequestAborted);
        if (tenant is null || !tenant.IsActive)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = $"Unknown or inactive tenant '{tenantId}'." });
            return;
        }

        ((TenantContext)tenantContext).SetTenant(tenant.Id);

        await _next(context);
    }
}
