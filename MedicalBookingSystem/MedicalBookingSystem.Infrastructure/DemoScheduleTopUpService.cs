using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedicalBookingSystem.Infrastructure;

/// <summary>Adds future working days for the demo doctor every few hours, so the public demo never runs out of slots.</summary>
public sealed class DemoScheduleTopUpService(IServiceScopeFactory scopes, ILogger<DemoScheduleTopUpService> logger, TimeProvider time) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var added = await scope.ServiceProvider.GetRequiredService<DataSeeder>().TopUpScheduleAsync(stoppingToken);
                if (added > 0) logger.LogInformation("Added {Count} demo schedule days", added);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A failed top-up must not stop the API; the next tick retries.
                logger.LogWarning(exception, "Demo schedule top-up failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
