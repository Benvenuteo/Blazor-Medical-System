namespace MedicalBookingSystem.SharedKernel.Dto;

/// <summary>ID token returned to the browser by Google Identity Services, with the nonce the page generated.</summary>
public sealed class GoogleSignInDto
{
    public string IdToken { get; set; } = string.Empty;
    public string Nonce { get; set; } = string.Empty;
}

public sealed class LinkGoogleDto
{
    public string IdToken { get; set; } = string.Empty;
    public string Nonce { get; set; } = string.Empty;
    public string CurrentPassword { get; set; } = string.Empty;
}

public sealed record ExternalLoginStatusDto(bool GoogleEnabled, bool GoogleLinked, string? GoogleEmail, bool HasPassword);

public sealed class CompleteProfileDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
}

public static class ExternalLoginErrors
{
    /// <summary>Stable code in ProblemDetails, so the UI can explain how to link an existing account.</summary>
    public const string AccountExists = "account_exists_link_required";
}
