using MedicalBookingSystem.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MedicalBookingSystem.IntegrationTests.Support;

/// <summary>Creates independent SQLite connections against one disposable file, matching real concurrent requests.</summary>
internal sealed class TemporarySqliteDatabase : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "medical-booking-tests", Guid.NewGuid().ToString("N"));
    public string ConnectionString { get; }
    public TemporarySqliteDatabase()
    {
        Directory.CreateDirectory(_directory);
        ConnectionString = $"Data Source={Path.Combine(_directory, "medical.db")};Pooling=False;Default Timeout=10";
    }
    public void Dispose() => Directory.Delete(_directory, recursive: true);
}

/// <summary>Both requests finish availability checks before either writes; no sleep-based race assumptions.</summary>
internal sealed class ConcurrentSaveGate : SaveChangesInterceptor
{
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _arrivals;
    public bool Active { get; set; }
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Active && eventData.Context!.ChangeTracker.Entries<Appointment>().Any(e => e.State is EntityState.Added or EntityState.Modified))
        {
            if (Interlocked.Increment(ref _arrivals) == 2) _ready.TrySetResult();
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        }
        return result;
    }
}

internal sealed class AdjustableTimeProvider(DateTimeOffset initial) : TimeProvider
{
    private long _ticks = initial.UtcTicks;
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
    public void SetUtcNow(DateTimeOffset value) => Interlocked.Exchange(ref _ticks, value.UtcTicks);
}
