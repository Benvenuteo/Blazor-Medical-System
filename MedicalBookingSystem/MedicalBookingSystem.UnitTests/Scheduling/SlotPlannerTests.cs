using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.Domain.Scheduling;

namespace MedicalBookingSystem.UnitTests.Scheduling;

public sealed class SlotPlannerTests
{
    private static readonly DateTime Day = new(2026, 11, 2);

    [Fact]
    public void Shift_IsSplitIntoFullHalfHourSlots_WithExclusiveEnd()
    {
        var slots = Slots([Shift(9, 11)]);
        Assert.Equal(new[] { Day.AddHours(9), Day.AddHours(9.5), Day.AddHours(10), Day.AddHours(10.5) }, slots);
    }

    [Fact]
    public void IrregularShiftStart_LeavesOnlyCompleteAlignedSlots()
    {
        var slots = Slots([new DoctorSchedule { StartTime = Day.AddHours(9).AddMinutes(15), EndTime = Day.AddHours(10).AddMinutes(15) }]);
        Assert.Equal(new[] { Day.AddHours(9.5) }, slots);
    }

    [Fact]
    public void OverlappingOrDuplicateShifts_DoNotDuplicateSlots()
    {
        Assert.Equal(4, Slots([Shift(9, 11), Shift(9, 11), Shift(10, 11)]).Count);
    }

    [Theory]
    [InlineData(AppointmentStatus.Scheduled, 3)]
    [InlineData(AppointmentStatus.Completed, 3)]
    [InlineData(AppointmentStatus.Cancelled, 4)]
    public void OnlyNonCancelledVisitsOccupyTheirInterval(AppointmentStatus status, int count)
    {
        var slots = Slots([Shift(9, 11)], [new Appointment { Date = Day.AddHours(9.5), Status = status }]);
        Assert.Equal(count, slots.Count);
        Assert.Contains(Day.AddHours(10), slots);
    }

    [Fact]
    public void LegacyVisitOffGrid_BlocksBothOverlappingSlots_ButAllowsTouchingIntervals()
    {
        var slots = Slots([Shift(9, 11)], [new Appointment { Date = Day.AddHours(9).AddMinutes(15) }]);
        Assert.Equal(new[] { Day.AddHours(10), Day.AddHours(10.5) }, slots);
    }

    [Fact]
    public void WindowBoundsAreInclusive_AndUnavailableShiftsAreIgnored()
    {
        var slots = AppointmentSlots.GetAvailableStarts([Shift(9, 11), new DoctorSchedule { StartTime = Day.AddHours(12), EndTime = Day.AddHours(13), IsAvailable = false }], [], Day.AddHours(9.5), Day.AddHours(10));
        Assert.Equal(new[] { Day.AddHours(9.5), Day.AddHours(10) }, slots);
    }

    [Fact]
    public void InvalidAndTooShortShifts_HaveNoSlots()
    {
        Assert.Empty(Slots([new DoctorSchedule(), new DoctorSchedule { StartTime = Day.AddHours(9), EndTime = Day.AddHours(9).AddMinutes(20) }]));
    }

    private static DoctorSchedule Shift(double from, double to) => new() { StartTime = Day.AddHours(from), EndTime = Day.AddHours(to) };
    private static IReadOnlyList<DateTime> Slots(IEnumerable<DoctorSchedule> shifts, IEnumerable<Appointment>? visits = null) =>
        AppointmentSlots.GetAvailableStarts(shifts, visits ?? [], Day, Day.AddDays(1));
}
