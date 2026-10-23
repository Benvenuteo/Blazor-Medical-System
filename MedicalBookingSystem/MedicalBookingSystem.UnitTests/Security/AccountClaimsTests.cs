using System.Security.Claims;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.SharedKernel.Security;
using MedicalBookingSystem.UnitTests.Support;

namespace MedicalBookingSystem.UnitTests.Security;

public sealed class AccountClaimsTests
{
    [Theory]
    [InlineData(Roles.Patient, null)]
    [InlineData(Roles.Doctor, 4)]
    [InlineData(Roles.Admin, null)]
    public void CanonicalIdentity_ReadsOneAccountWithItsOwnRole(string role, int? doctorId)
    {
        var stamp = Guid.NewGuid();
        var principal = AccountClaims.Create(2, "Account", "test@example.test", role, DateTimeOffset.UtcNow.AddHours(1), doctorId, stamp);
        Assert.True(AccountClaims.TryReadIdentity(principal, out var identity));
        Assert.Equal(2, identity.Id); Assert.Equal(role, identity.Role); Assert.Equal(stamp, identity.SecurityStamp); Assert.Equal(doctorId, identity.DoctorId);
    }

    [Theory]
    [InlineData("missing-stamp")]
    [InlineData("empty-stamp")]
    [InlineData("duplicate-stamp")]
    [InlineData("duplicate-role")]
    [InlineData("duplicate-subject")]
    [InlineData("foreign-patient")]
    [InlineData("extra-doctor")]
    [InlineData("patient-password-flag")]
    [InlineData("wrong-subject-prefix")]
    public void AmbiguousOrIncompleteIdentity_IsRejected(string change)
    {
        var principal = AccountClaims.Create(2, "Account", "test@example.test", Roles.Patient, DateTimeOffset.UtcNow.AddHours(1), securityStamp: Guid.NewGuid());
        var identity = (ClaimsIdentity)principal.Identity!;
        switch (change)
        {
            case "missing-stamp": identity.RemoveClaim(identity.FindFirst(AccountClaims.SecurityStamp)!); break;
            case "empty-stamp": identity.RemoveClaim(identity.FindFirst(AccountClaims.SecurityStamp)!); identity.AddClaim(new(AccountClaims.SecurityStamp, Guid.Empty.ToString("N"))); break;
            case "duplicate-stamp": identity.AddClaim(new(AccountClaims.SecurityStamp, Guid.NewGuid().ToString("N"))); break;
            case "duplicate-role": identity.AddClaim(new(ClaimTypes.Role, Roles.Admin)); break;
            case "duplicate-subject": identity.AddClaim(new(ClaimTypes.NameIdentifier, "patient:3")); break;
            case "foreign-patient": identity.RemoveClaim(identity.FindFirst(AccountClaims.PatientId)!); identity.AddClaim(new(AccountClaims.PatientId, "3")); break;
            case "extra-doctor": identity.AddClaim(new(AccountClaims.DoctorId, "4")); break;
            case "patient-password-flag": identity.AddClaim(new(AccountClaims.PasswordChangeRequired, "true")); break;
            case "wrong-subject-prefix": identity.RemoveClaim(identity.FindFirst(ClaimTypes.NameIdentifier)!); identity.AddClaim(new(ClaimTypes.NameIdentifier, "staff:2")); break;
        }
        Assert.False(AccountClaims.TryReadIdentity(principal, out _));
    }

    [Theory]
    [InlineData(SessionStatus.Invalid, false)]
    [InlineData(SessionStatus.PasswordChangeRequired, true)]
    public async Task SessionGuard_DistinguishesRevokedSessionsFromRestrictedCredentials(SessionStatus status, bool authenticated)
    {
        var clock = new TestTimeProvider().CreateClock();
        var principal = AccountClaims.Create(1, "Account", "test@example.test", Roles.Admin, new DateTimeOffset(clock.UtcNow).AddHours(1), securityStamp: Guid.NewGuid());
        var guard = new SessionGuard(clock, new FixedSessionValidator(status));
        var denied = await Assert.ThrowsAsync<AccessDeniedException>(() => guard.RequireAsync(principal));
        Assert.Equal(authenticated, denied.Authenticated);
        if (status == SessionStatus.PasswordChangeRequired) Assert.Same(principal, await guard.RequireAsync(principal, allowPasswordChange: true));
        else await Assert.ThrowsAsync<AccessDeniedException>(() => guard.RequireAsync(principal, allowPasswordChange: true));
    }
    private sealed class FixedSessionValidator(SessionStatus status) : ISessionValidator
    { public Task<SessionStatus> ValidateAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default) => Task.FromResult(status); }
}
