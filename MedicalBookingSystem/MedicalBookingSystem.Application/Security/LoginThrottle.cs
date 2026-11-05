namespace MedicalBookingSystem.Application.Security;

/// <summary>
/// Per-account protection against password guessing, in addition to the per-address rate limit.
/// After <see cref="MaxFailures"/> wrong passwords the account rejects logins for <see cref="LockoutDuration"/>.
/// Demo accounts are exempt: anyone could otherwise lock the public demo for every other visitor.
/// The caller always returns the same message, so a lockout does not reveal that an account exists.
/// </summary>
public static class LoginThrottle
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    public const string FailureMessage = "Nieprawidłowy email lub hasło. Po kilku nieudanych próbach logowanie jest na chwilę blokowane.";

    public static bool IsLockedOut(DateTime? lockoutEndUtc, bool isDemo, DateTime nowUtc) =>
        !isDemo && lockoutEndUtc is { } end && end > nowUtc;

    /// <summary>Returns the new counter and lockout end after a wrong password.</summary>
    public static (int Failures, DateTime? LockoutEndUtc) RegisterFailure(int failures, bool isDemo, DateTime nowUtc)
    {
        if (isDemo) return (0, null);
        failures++;
        return failures >= MaxFailures ? (0, nowUtc + LockoutDuration) : (failures, null);
    }
}
