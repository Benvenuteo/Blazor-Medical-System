using System.Globalization;
using System.Text;

namespace MedicalBookingSystem.Application.Notifications;

/// <summary>Minimal iCalendar (RFC 5545) event. Times are written in UTC, so no time-zone definition is needed.</summary>
public static class CalendarInvite
{
    public const string ContentType = "text/calendar; charset=utf-8; method=PUBLISH";

    public static string Create(int appointmentId, DateTime startUtc, DateTime endUtc, string summary, string? location,
        string description, DateTime nowUtc, string uidDomain = "medical-booking.example")
    {
        var lines = new List<string>
        {
            "BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//Medical Booking System//PL", "CALSCALE:GREGORIAN", "METHOD:PUBLISH",
            "BEGIN:VEVENT",
            $"UID:appointment-{appointmentId}@{uidDomain}",
            $"DTSTAMP:{Utc(nowUtc)}", $"DTSTART:{Utc(startUtc)}", $"DTEND:{Utc(endUtc)}",
            $"SUMMARY:{Escape(summary)}", $"DESCRIPTION:{Escape(description)}"
        };
        if (!string.IsNullOrWhiteSpace(location)) lines.Add($"LOCATION:{Escape(location)}");
        lines.AddRange(["BEGIN:VALARM", "ACTION:DISPLAY", "TRIGGER:-PT2H", $"DESCRIPTION:{Escape(summary)}", "END:VALARM", "END:VEVENT", "END:VCALENDAR"]);
        var builder = new StringBuilder();
        foreach (var line in lines) Fold(builder, line);
        return builder.ToString();
    }

    private static string Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string Escape(string value) => value
        .Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\\n").Replace("\n", "\\n");

    /// <summary>Lines longer than 75 octets continue on the next line after CRLF and a space.</summary>
    private static void Fold(StringBuilder builder, string line)
    {
        var bytes = 0;
        foreach (var rune in line.EnumerateRunes())
        {
            var size = rune.Utf8SequenceLength;
            if (bytes + size > 74) { builder.Append("\r\n "); bytes = 1; }
            builder.Append(rune.ToString());
            bytes += size;
        }
        builder.Append("\r\n");
    }
}
