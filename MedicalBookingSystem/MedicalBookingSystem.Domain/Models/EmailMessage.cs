namespace MedicalBookingSystem.Domain.Models;

/// <summary>
/// Transactional outbox: a message is stored in the same database transaction as the change that
/// caused it and delivered later by a background sender, so a mail server outage never fails or
/// half-commits a user action.
/// </summary>
public sealed class EmailMessage
{
    public int Id { get; set; }
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string TextBody { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string? AttachmentName { get; set; }
    public string? AttachmentContentType { get; set; }
    public string? AttachmentContent { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
