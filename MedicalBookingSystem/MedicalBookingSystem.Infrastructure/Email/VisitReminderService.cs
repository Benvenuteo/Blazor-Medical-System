using MedicalBookingSystem.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedicalBookingSystem.Infrastructure.Email;

/// <summary>Queues day-before reminders every hour; each visit is reminded once (ReminderQueuedAtUtc).</summary>
public sealed class VisitReminderService(IServiceScopeFactory scopes, TimeProvider time, ILogger<VisitReminderService> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    /// <summary>Lets the host finish starting (and migrations run) before the first pass touches the database.</summary>
    public static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(InitialDelay, time, stoppingToken);
        using var timer = new PeriodicTimer(Interval, time);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var queued = await scope.ServiceProvider.GetRequiredService<VisitNotificationService>().QueueTomorrowRemindersAsync();
                if (queued > 0) logger.LogInformation("Queued {Count} visit reminders", queued);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Visit reminder run failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
