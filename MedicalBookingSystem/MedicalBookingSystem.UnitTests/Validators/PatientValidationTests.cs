using MedicalBookingSystem.Application.Validators;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.UnitTests.Support;

namespace MedicalBookingSystem.UnitTests.Validators;

public sealed class PatientValidationTests
{
    [Theory]
    [InlineData("", true)]
    [InlineData("600700800", true)]
    [InlineData("+48 600 700 800", true)]
    [InlineData("600-700-800", true)]
    [InlineData("60070080", false)]
    [InlineData("--- --- ---", false)]
    [InlineData("phone 600700800", false)]
    [InlineData("+48 600 700 800 900 1", false)]
    public void Registration_ValidatesOptionalPhoneNumber(string phone, bool expected)
    {
        var validator = new RegisterPatientDtoValidator(new TestTimeProvider().CreateClock());

        var result = validator.Validate(ValidRegistration(phone: phone));

        Assert.Equal(expected, result.IsValid);
    }

    [Theory]
    [InlineData("600700800", true)]
    [InlineData("--- --- ---", false)]
    public void ProfileUpdate_UsesSamePhoneRuleAsRegistration(string phone, bool expected)
    {
        var result = new UpdatePatientDtoValidator().Validate(new UpdatePatientDto { PhoneNumber = phone });

        Assert.Equal(expected, result.IsValid);
    }

    [Theory]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void RegistrationAndProfileUpdate_ShareMaximumPasswordLength(int length, bool expected)
    {
        var password = "A1" + new string('a', length - 2);
        var registration = new RegisterPatientDtoValidator(new TestTimeProvider().CreateClock())
            .Validate(ValidRegistration(password: password));
        var update = new UpdatePatientDtoValidator().Validate(new UpdatePatientDto
        {
            CurrentPassword = "Current1", NewPassword = password, ConfirmNewPassword = password
        });

        Assert.Equal(expected, registration.IsValid);
        Assert.Equal(expected, update.IsValid);
    }

    private static RegisterPatientDto ValidRegistration(string phone = "", string password = "Patient2026!") => new()
    {
        Email = "patient@example.test", Password = password, FirstName = "Anna", LastName = "Nowak",
        DateOfBirth = new DateTime(1990, 1, 1), PhoneNumber = phone
    };
}
