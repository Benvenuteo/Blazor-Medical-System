using MedicalBookingSystem.Application.Time;

namespace MedicalBookingSystem.UnitTests.Support;

internal sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan elapsed) => _now = _now.Add(elapsed);
    public ClinicClock CreateClock() => new(this, TimeZoneInfo.Utc);
}
