using MedicalBookingSystem.Domain.Contracts;

namespace MedicalBookingSystem.BlazorServer.Service;

/// <summary>Recovers files left by crashes or concurrent replacements; ignores files less than 24 hours old.</summary>
public sealed class DoctorPhotoCleanup(IServiceScopeFactory scopes, ILogger<DoctorPhotoCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await SweepAsync(scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>(),
                    scope.ServiceProvider.GetRequiredService<DoctorPhotoFiles>(), DateTime.UtcNow.AddDays(-1));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            { logger.LogWarning(exception, "Doctor photo cleanup failed; it will retry in one hour."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public static async Task SweepAsync(IUnitOfWorkFactory units, DoctorPhotoFiles files, DateTime olderThanUtc)
    {
        using var unit = units.Create();
        var urls = (await unit.Doctors.GetAllAsync()).Select(d => d.ImageUrl).ToHashSet(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(files.Directory))
        {
            var name = Path.GetFileName(path);
            var pending = name.EndsWith(".pending", StringComparison.Ordinal);
            if (!DoctorPhotoFiles.IsOwnedName(pending ? name[..^8] : name) || File.GetLastWriteTimeUtc(path) >= olderThanUtc) continue;
            if (pending || !urls.Contains(files.Url(name))) files.TryDeletePath(path);
        }
    }
}
