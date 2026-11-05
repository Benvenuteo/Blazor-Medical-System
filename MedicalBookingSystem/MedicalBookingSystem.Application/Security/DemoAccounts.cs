using MedicalBookingSystem.SharedKernel;

namespace MedicalBookingSystem.Application.Security;

/// <summary>
/// Demo accounts are shared by every visitor of the public demo. Changing their password, email,
/// profile or sessions would lock out or log out the next visitor, so those operations are refused.
/// Everything else (booking, documentation, reviews, administration of other accounts) works normally.
/// </summary>
public static class DemoAccounts
{
    public const string Message = "To publiczne konto demonstracyjne — jego danych, hasła i sesji nie można zmieniać. Pozostałe funkcje działają normalnie.";
    public const string PatientEmail = "patient@example.test";
    public const string DoctorEmail = "doctor@example.test";
    public const string AdminEmail = "admin@example.test";
    public static readonly IReadOnlyList<string> StaffEmails = [DoctorEmail, AdminEmail];

    public static Result Refused() => Result.Failure(Message, ErrorType.Forbidden);
    public static Result<T> Refused<T>() => Result.Failure<T>(Message, ErrorType.Forbidden);
}
