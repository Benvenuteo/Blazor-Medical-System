using System.Text;
using MedicalBookingSystem.Application.Notifications;

namespace MedicalBookingSystem.UnitTests.Notifications;

public sealed class CalendarInviteTests
{
    private static readonly DateTime Start = new(2026, 11, 20, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Event_UsesUtcTimes_AStableUid_AndCrlfLines()
    {
        var ics = CalendarInvite.Create(42, Start, Start.AddMinutes(30), "Wizyta: Anna Demonstracyjna", "Centrum, ul. Floriańska 10",
            "Opis", new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc));

        Assert.StartsWith("BEGIN:VCALENDAR\r\n", ics);
        Assert.EndsWith("END:VCALENDAR\r\n", ics);
        Assert.Contains("UID:appointment-42@", ics);
        Assert.Contains("DTSTART:20261120T093000Z\r\n", ics);
        Assert.Contains("DTEND:20261120T100000Z\r\n", ics);
        Assert.DoesNotContain("\n", ics.Replace("\r\n", ""));
    }

    [Fact]
    public void Text_IsEscaped_SoAddressesCannotBreakTheStructure()
    {
        var ics = CalendarInvite.Create(1, Start, Start.AddMinutes(30), "A; B, C", "Ul. X\nLINE:INJECTED", "x\\y", Start);

        Assert.Contains("SUMMARY:A\\; B\\, C", ics);
        Assert.Contains("LOCATION:Ul. X\\nLINE:INJECTED", ics);
        Assert.Contains("DESCRIPTION:x\\\\y", ics);
        Assert.DoesNotContain("\r\nLINE:INJECTED", ics);
    }

    [Fact]
    public void LongLines_AreFoldedAt75Octets_WithoutSplittingPolishCharacters()
    {
        var description = string.Concat(Enumerable.Repeat("Zażółć gęślą jaźń ", 12));

        var ics = CalendarInvite.Create(1, Start, Start.AddMinutes(30), "Wizyta", null, description, Start);
        var physical = ics.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var unfolded = ics.Replace("\r\n ", "");

        Assert.All(physical, line => Assert.True(Encoding.UTF8.GetByteCount(line) <= 75, line));
        Assert.Contains("DESCRIPTION:" + description.TrimEnd(), unfolded);
    }
}
