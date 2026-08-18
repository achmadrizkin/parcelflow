using System.Text.RegularExpressions;
using Xunit;

namespace ParcelFlow.Tests;

/// <summary>
/// Enforces architectural invariants that the compiler can't check on its
/// own. PF-1287 (cross-carrier data in the daily summary report) happened
/// because <c>ITenantScopedRepository&lt;T&gt;.QueryAllTenantsAsync</c> —
/// documented as migration-tooling-only and "must never be called from
/// request-handling code paths" — was called from
/// <c>ReportService</c>. See docs/adr/0002-tenant-isolation-by-tenantid.md
/// and docs/adr/0005-close-pf902-tenant-scope-reportservice.md.
///
/// This test turns that "must never" from a doc comment a developer has to
/// remember into something the build actually enforces, so the same class
/// of bug (an unscoped query slipping into request-handling code) fails the
/// test suite instead of shipping.
/// </summary>
public class ArchitectureRulesTests
{
    private static readonly string[] RequestHandlingProjects = { "ParcelFlow.Services", "ParcelFlow.Api" };
    private static readonly Regex UnsafeCallPattern = new(@"\.QueryAllTenantsAsync\s*\(", RegexOptions.Compiled);

    [Fact]
    public void QueryAllTenantsAsync_is_never_called_from_request_handling_projects()
    {
        var repoRoot = LocateRepoRoot();
        var offenders = new List<string>();

        foreach (var project in RequestHandlingProjects)
        {
            var projectDir = Path.Combine(repoRoot, "src", project);
            foreach (var file in Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories))
            {
                if (UnsafeCallPattern.IsMatch(File.ReadAllText(file)))
                {
                    offenders.Add(Path.GetRelativePath(repoRoot, file));
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "QueryAllTenantsAsync must never be called from request-handling code " +
            "(ParcelFlow.Services / ParcelFlow.Api) - it bypasses tenant isolation " +
            "and is reserved for migration tooling (see PF-1287, ADR-0002, ADR-0005). " +
            "Offending file(s): " + string.Join(", ", offenders));
    }

    /// <summary>Walks up from the test binary's output directory to find the repo root.</summary>
    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ParcelFlow.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repo root (ParcelFlow.sln) walking up from {AppContext.BaseDirectory}.");
    }
}
