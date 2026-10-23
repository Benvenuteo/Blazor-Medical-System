using System.Security.Claims;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.SharedKernel.Security;

namespace MedicalBookingSystem.UnitTests.Support;

internal sealed class TestUsers(ClaimsPrincipal principal) : ICurrentUser
{
    public Task<ClaimsPrincipal> GetPrincipalAsync() => Task.FromResult(principal);
    public static UserAccess Patient(int id = 2) => Access(Roles.Patient, id);
    public static UserAccess Admin() => Access(Roles.Admin, 1);
    public static UserAccess Doctor(int id) => Access(Roles.Doctor, 1, id);
    public static UserAccess Access(string role, int id, int? doctorId = null)
    {
        var clock = new TestTimeProvider().CreateClock();
        return new UserAccess(new TestUsers(AccountClaims.Create(id, "Test", "test@example.test", role,
            new DateTimeOffset(clock.UtcNow).AddHours(1), doctorId, Guid.NewGuid())), new SessionGuard(clock, new ValidSessionValidator()));
    }
}

internal sealed class ValidSessionValidator : ISessionValidator
{
    public Task<SessionStatus> ValidateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default) => Task.FromResult(SessionStatus.Valid);
}
