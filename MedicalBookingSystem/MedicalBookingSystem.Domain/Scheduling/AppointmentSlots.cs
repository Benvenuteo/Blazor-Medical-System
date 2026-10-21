using MedicalBookingSystem.Domain.Models;

namespace MedicalBookingSystem.Domain.Scheduling;

/// <summary>Appointments occupy half-open, 30-minute intervals on the clinic's local clock.</summary>
public static class AppointmentSlots
{
    public const int DurationMinutes = 30;
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(DurationMinutes);
    public static bool IsAligned(DateTime start) => start.Ticks % Duration.Ticks == 0;

    public static DateTime RoundUp(DateTime value)
    {
        var remainder = value.Ticks % Duration.Ticks;
        return remainder == 0 ? value : value.AddTicks(Duration.Ticks - remainder);
    }

    /// <summary>Deduplicates shifts, then sweeps sorted occupied intervals; touching endpoints are allowed.</summary>
    public static IReadOnlyList<DateTime> GetAvailableStarts(IEnumerable<DoctorSchedule> schedules,
        IEnumerable<Appointment> appointments, DateTime fromInclusive, DateTime toInclusive)
    {
        if (fromInclusive > toInclusive) return [];
        var candidates = new SortedSet<DateTime>();
        foreach (var schedule in schedules.Where(s => s.IsAvailable))
        {
            if (schedule.EndTime.Ticks < Duration.Ticks) continue;
            var latest = schedule.EndTime.Subtract(Duration);
            var first = schedule.StartTime > fromInclusive ? schedule.StartTime : fromInclusive;
            if (first > latest) continue;
            var start = RoundUp(first);
            if (latest > toInclusive) latest = toInclusive;
            for (; start <= latest; start = start.Add(Duration)) candidates.Add(start);
        }
        var busy = appointments.Where(a => a.Status != AppointmentStatus.Cancelled).OrderBy(a => a.Date).ToArray();
        var result = new List<DateTime>();
        var index = 0;
        foreach (var candidate in candidates)
        {
            while (index < busy.Length && busy[index].Date.Add(Duration) <= candidate) index++;
            if (index == busy.Length || busy[index].Date >= candidate.Add(Duration)) result.Add(candidate);
        }
        return result;
    }
}
