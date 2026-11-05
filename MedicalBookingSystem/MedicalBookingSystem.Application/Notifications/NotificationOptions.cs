namespace MedicalBookingSystem.Application.Notifications;

public sealed class NotificationOptions
{
    /// <summary>Public address of the patient app; links in emails point there.</summary>
    public string PatientAppUrl { get; set; } = "http://localhost:5016";

    public string Link(string path) => PatientAppUrl.TrimEnd('/') + path;
}
