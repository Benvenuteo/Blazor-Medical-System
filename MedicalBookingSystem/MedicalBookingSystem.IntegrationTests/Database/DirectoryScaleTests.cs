using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.Domain.Scheduling;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.IntegrationTests.Support;
using MedicalBookingSystem.SharedKernel.Dto.DtoHelp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace MedicalBookingSystem.IntegrationTests.Database;

public sealed class DirectoryScaleTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Performance")]
    public async Task DirectoryAtScale_BoundsSqlRowsAndDoesNotTrackEntityGraphs()
    {
        using var database = new TemporarySqliteDatabase();
        var commands = new QueryCapture();
        using var factory = new MedicalApiFactory(database.ConnectionString, interceptor: commands);
        await factory.InitializeDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<ClinicClock>();
        var reviewers = Enumerable.Range(0, 10).Select(i => new Patient { Email = $"reviewer{i}@example.test", FirstName = "Review", LastName = "Demo", PasswordHash = "not used" }).ToArray();
        for (var i = 0; i < 100; i++)
        {
            var doctor = new Doctor { FirstName = "Doctor", LastName = $"Demo{i:000}", LicenseNumber = $"SCALE-{i}", Region = "Scale" };
            var patient = new Patient { Email = $"scale{i}@example.test", FirstName = "Patient", LastName = "Demo", PasswordHash = "not used" };
            for (var day = 2; day < 16; day++) doctor.Schedules.Add(new DoctorSchedule { StartTime = clock.Today.AddDays(day).AddHours(9), EndTime = clock.Today.AddDays(day).AddHours(16) });
            for (var old = 1; old <= 10; old++) doctor.Appointments.Add(new Appointment { Patient = patient, Date = clock.Today.AddDays(-old).AddHours(9), Status = AppointmentStatus.Completed });
            for (var review = 0; review < 10; review++) doctor.Reviews.Add(new Review { Patient = reviewers[review], Rating = review % 5 + 1 });
            db.Doctors.Add(doctor);
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = scope.ServiceProvider.GetRequiredService<IDoctorService>();
        var search = new SearchDoctorsDto { Region = "Scale", SortBy = "availability", PageSize = 10, Page = 3 };
        await service.SearchDoctorsAsync(search); // Warm JIT and EF query compilation outside the measured sample.
        commands.Active = true;
        var watch = Stopwatch.StartNew();
        var result = await service.SearchDoctorsAsync(search);
        watch.Stop();
        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value.TotalCount);
        Assert.Equal(10, result.Value.Items.Count);
        Assert.Equal(2, commands.Sql.Count);
        Assert.Contains(commands.Sql, sql => sql.Contains("LIMIT", StringComparison.Ordinal) && sql.Contains("OFFSET", StringComparison.Ordinal));
        Assert.All(result.Value.Items, d => Assert.Equal(10, d.ReviewCount));
        Assert.Empty(db.ChangeTracker.Entries());
        commands.Active = false;
        var current = new List<double> { watch.Elapsed.TotalMilliseconds };
        for (var run = 0; run < 4; run++) { watch.Restart(); await service.SearchDoctorsAsync(search); watch.Stop(); current.Add(watch.Elapsed.TotalMilliseconds); }
        // Reproduce the previous catalog graph query for a documented local comparison, never for production reads.
        var legacy = new List<double>();
        for (var run = 0; run < 3; run++)
        {
            watch.Restart();
            var graph = await db.Doctors.AsNoTracking().Include(d => d.Reviews).Include(d => d.Schedules)
                .Include(d => d.Appointments).Include(d => d.DoctorSpecializations).ThenInclude(s => s.Specialization).AsSingleQuery().ToListAsync();
            var oldPage = graph.Select(d => new { d.Id, Next = AppointmentSlots.GetAvailableStarts(d.Schedules, d.Appointments, clock.MinimumBookingTime, clock.Now.AddMonths(3)).FirstOrDefault() })
                .OrderBy(d => d.Next).ThenBy(d => d.Id).Skip(20).Take(10).ToList();
            watch.Stop(); legacy.Add(watch.Elapsed.TotalMilliseconds); Assert.Equal(result.Value.Items.Select(d => d.Id), oldPage.Select(d => d.Id));
        }
        var report = JsonSerializer.Serialize(new
        {
            scenario = "100 doctors, 1400 shifts, 1000 historical appointments, 1000 reviews; availability page 3 / 10",
            provider = "SQLite file, Release, warm current query", runtime = Environment.Version.ToString(),
            currentMilliseconds = current, previousGraphMilliseconds = legacy,
            currentMedianMilliseconds = current.Order().ElementAt(2), previousMedianMilliseconds = legacy.Order().ElementAt(1),
            currentSelectStatements = 2, currentPageRows = 10, currentTrackedEntities = 0,
            previousJoinedRows = 140000, note = "Diagnostic samples, not a production capacity claim; no timing threshold in CI."
        }, new JsonSerializerOptions { WriteIndented = true });
        output.WriteLine(report);
        var path = Environment.GetEnvironmentVariable("MEDICAL_BENCHMARK_OUTPUT");
        if (!string.IsNullOrWhiteSpace(path)) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); await File.WriteAllTextAsync(path, report); }
    }

    private sealed class QueryCapture : DbCommandInterceptor
    {
        public bool Active { get; set; }
        public ConcurrentBag<string> Sql { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Active) Sql.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
