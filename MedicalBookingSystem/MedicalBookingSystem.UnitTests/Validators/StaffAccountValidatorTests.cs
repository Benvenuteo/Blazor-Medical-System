using MedicalBookingSystem.Application.Validators;
using MedicalBookingSystem.SharedKernel.Dto.Accounts;

namespace MedicalBookingSystem.UnitTests.Validators;

public sealed class StaffAccountValidatorTests
{
    [Theory]
    [InlineData("Short2026!")]
    [InlineData("lowercaseonly2026!")]
    [InlineData("NoDigitsInThisPassword!")]
    public void TemporaryStaffPassword_RequiresLengthUppercaseAndDigit(string password)
    {
        var dto = new CreateStaffAccountDto { Email = "staff@example.test", DisplayName = "Staff", Role = "Admin", InitialPassword = password };
        Assert.False(new CreateStaffAccountValidator().Validate(dto).IsValid);
        Assert.False(new ResetStaffPasswordValidator().Validate(new ResetStaffPasswordDto { NewPassword = password, Version = Guid.NewGuid() }).IsValid);
    }

    [Theory]
    [InlineData("Patient", null, false)]
    [InlineData("Doctor", null, false)]
    [InlineData("Doctor", 0, false)]
    [InlineData("Doctor", 4, true)]
    [InlineData("Admin", 4, false)]
    [InlineData("Admin", null, true)]
    public void RoleAndDoctorAssociation_MustDescribeOneStaffIdentity(string role, int? doctorId, bool valid)
    {
        var dto = new CreateStaffAccountDto { Email = "staff@example.test", DisplayName = "Staff", Role = role, DoctorId = doctorId, InitialPassword = "TemporaryStaff2026!" };
        Assert.Equal(valid, new CreateStaffAccountValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void StaffPasswordChange_RequiresMatchingConfirmation()
    {
        Assert.False(new ChangeStaffPasswordValidator().Validate(new ChangeStaffPasswordDto
        { CurrentPassword = "OriginalStaff2026!", NewPassword = "NewPassword2026!", ConfirmNewPassword = "DifferentPassword2026!" }).IsValid);
    }
}
