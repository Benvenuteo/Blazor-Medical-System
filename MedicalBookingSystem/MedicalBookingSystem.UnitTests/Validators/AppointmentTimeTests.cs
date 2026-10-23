using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Application.Validators;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;
using MedicalBookingSystem.UnitTests.Support;

namespace MedicalBookingSystem.UnitTests.Validators;

public sealed class AppointmentTimeTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(15, false)]
    [InlineData(30, true)]
    [InlineData(45, false)]
    public void OnlyFullAndHalfHoursAreValid(int minute, bool expected)
    {
        var clock = new TestTimeProvider().CreateClock();
        var date = clock.Now.AddDays(2).AddMinutes(minute);
        Assert.Equal(expected, new CreateAppointmentDtoValidator(clock).Validate(new CreateAppointmentDto { DoctorId = 1, Date = date }).IsValid);
        Assert.Equal(expected, new UpdateAppointmentDtoValidator(clock).Validate(new UpdateAppointmentDto { NewDate = date }).IsValid);
    }

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void ExplicitOffsetsAreRejected_InsteadOfBeingSilentlyTreatedAsClinicTime(DateTimeKind kind)
    {
        var clock = new TestTimeProvider().CreateClock();
        var result = new CreateAppointmentDtoValidator(clock).Validate(new CreateAppointmentDto { DoctorId = 1, Date = DateTime.SpecifyKind(clock.Now.AddDays(2), kind) });
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(1, 30, false)]
    [InlineData(2, 0, false)]
    [InlineData(2, 30, false)]
    [InlineData(3, 0, true)]
    public void AutumnDstTransition_RejectsAnyVisitTouchingAmbiguousHour(int hour, int minute, bool expected)
    {
        var clock = new ClinicClock(new TestTimeProvider(), TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw"));
        var result = new CreateAppointmentDtoValidator(clock).Validate(new CreateAppointmentDto { DoctorId = 1, Date = new DateTime(2026, 10, 25, hour, minute, 0) });
        Assert.Equal(expected, result.IsValid);
    }

    [Theory]
    [InlineData(1, 30, false)]
    [InlineData(2, 0, false)]
    [InlineData(2, 30, false)]
    [InlineData(3, 0, true)]
    public void SpringDstTransition_RejectsMissingLocalTimes(int hour, int minute, bool expected)
    {
        var time = new TestTimeProvider();
        time.Advance(new DateTimeOffset(2027, 3, 24, 12, 0, 0, TimeSpan.Zero) - time.GetUtcNow());
        var clock = new ClinicClock(time, TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw"));
        var result = new CreateAppointmentDtoValidator(clock).Validate(new CreateAppointmentDto { DoctorId = 1, Date = new DateTime(2027, 3, 28, hour, minute, 0) });
        Assert.Equal(expected, result.IsValid);
    }

    [Fact]
    public void MinimumLeadTime_Is24ElapsedHours_AcrossSpringClockChange()
    {
        var time = new TestTimeProvider();
        time.Advance(new DateTimeOffset(2027, 3, 27, 2, 30, 0, TimeSpan.Zero) - time.GetUtcNow());
        var clock = new ClinicClock(time, TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw"));
        var validator = new CreateAppointmentDtoValidator(clock);
        Assert.False(validator.Validate(new CreateAppointmentDto { DoctorId = 1, Date = new(2027, 3, 28, 3, 30, 0) }).IsValid);
        Assert.True(validator.Validate(new CreateAppointmentDto { DoctorId = 1, Date = new(2027, 3, 28, 4, 30, 0) }).IsValid);
    }

    [Fact]
    public void Rescheduling_AlsoEnforcesThreeMonthHorizon()
    {
        var clock = new TestTimeProvider().CreateClock();
        Assert.False(new UpdateAppointmentDtoValidator(clock).Validate(new UpdateAppointmentDto { NewDate = clock.Now.AddMonths(3).AddMinutes(30) }).IsValid);
    }
}
