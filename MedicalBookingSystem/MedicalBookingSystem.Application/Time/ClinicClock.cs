namespace MedicalBookingSystem.Application.Time;

/// <summary>
/// Provides one controlled clock. Appointment and schedule values use clinic local time;
/// creation timestamps and token expiry use UTC.
/// </summary>
public sealed class ClinicClock(TimeProvider timeProvider, TimeZoneInfo timeZone)
{
    public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;
    public DateTime Now => TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone).DateTime;
    public DateTime Today => Now.Date;
    public DateTime MinimumBookingTime => TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow().AddHours(24), timeZone).DateTime;

    public bool IsUnambiguousLocalTime(DateTime value) =>
        !timeZone.IsInvalidTime(DateTime.SpecifyKind(value, DateTimeKind.Unspecified)) &&
        !timeZone.IsAmbiguousTime(DateTime.SpecifyKind(value, DateTimeKind.Unspecified));
}
