using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MedicalBookingSystem.Infrastructure.Hosting;

/// <summary>
/// Readiness means the shared SQLite database can be opened and its schema is current. SQLite creates
/// a missing file on open, so connecting alone would report an empty database as ready.
/// </summary>
public sealed class DatabaseHealthCheck(IDbContextFactory<ApplicationDbContext> contexts) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var database = await contexts.CreateDbContextAsync(cancellationToken);
            if (!await database.Database.CanConnectAsync(cancellationToken))
                return HealthCheckResult.Unhealthy("Database is unavailable.");
            var pending = (await database.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
            return pending == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Database schema is behind by {pending} migration(s).");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Database check failed.", exception);
        }
    }
}

public static class HealthEndpoints
{
    public static IServiceCollection AddDatabaseHealthCheck(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
        return services;
    }

    /// <summary>Maps anonymous liveness (process up) and readiness (database usable) probes.</summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/live", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
        // Status text only: exception details stay in the logs, not in a public endpoint.
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
            .AllowAnonymous();
        return endpoints;
    }
}
