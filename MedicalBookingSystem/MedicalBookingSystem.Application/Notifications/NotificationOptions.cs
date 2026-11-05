namespace MedicalBookingSystem.Application.Notifications;

public sealed class NotificationOptions
{
    /// <summary>Public address of the patient app; links in emails point there.</summary>
    public string PatientAppUrl { get; set; } = "http://localhost:5016";

    public string Link(string path) => PatientAppUrl.TrimEnd('/') + path;
}

/// <summary>Video visits use Jitsi Meet rooms: free, no accounts or keys; the room name is random and unguessable.</summary>
public static class VideoRooms
{
    public const string BaseUrl = "https://meet.jit.si/";

    public static string NewRoom() => "MedicalBooking-" + Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(12));

    public static string? Link(string? room) => room is null ? null : BaseUrl + room;
}
