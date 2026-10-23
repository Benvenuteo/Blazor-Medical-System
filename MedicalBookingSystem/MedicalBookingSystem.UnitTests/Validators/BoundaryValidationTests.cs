using MedicalBookingSystem.Application.Validators;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;
using MedicalBookingSystem.SharedKernel.Dto.NotesPrescriptionDto;
using MedicalBookingSystem.SharedKernel.Dto.ReviewsDto;
using MedicalBookingSystem.UnitTests.Support;

namespace MedicalBookingSystem.UnitTests.Validators;

public sealed class BoundaryValidationTests
{
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void CreateAppointment_EnforcesInclusive24HourBoundary(int secondsFromBoundary, bool expected)
    {
        var time = new TestTimeProvider();
        var fixedDate = time.CreateClock().Now;
        time.Advance(TimeSpan.FromSeconds(-secondsFromBoundary));
        var clock = time.CreateClock();
        var validator = new CreateAppointmentDtoValidator(clock);

        var result = validator.Validate(new CreateAppointmentDto
        {
            DoctorId = 1, Date = fixedDate.AddHours(24)
        });

        Assert.Equal(expected, result.IsValid);
    }

    [Fact]
    public void AppointmentValidator_UsesCurrentTimeOnEveryValidation()
    {
        var time = new TestTimeProvider();
        var clock = time.CreateClock();
        var validator = new CreateAppointmentDtoValidator(clock);
        var appointment = new CreateAppointmentDto { DoctorId = 1, Date = clock.Now.AddHours(25) };
        Assert.True(validator.Validate(appointment).IsValid);

        time.Advance(TimeSpan.FromHours(2));

        Assert.False(validator.Validate(appointment).IsValid);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void CreateAppointment_EnforcesThreeMonthLimit(int secondsFromBoundary, bool expected)
    {
        var time = new TestTimeProvider();
        var fixedDate = time.CreateClock().Now;
        time.Advance(TimeSpan.FromSeconds(-secondsFromBoundary));
        var clock = time.CreateClock();
        var validator = new CreateAppointmentDtoValidator(clock);
        var result = validator.Validate(new CreateAppointmentDto
        {
            DoctorId = 1, Date = fixedDate.AddMonths(3)
        });
        Assert.Equal(expected, result.IsValid);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void RegisterPatient_RequiresAtLeast18Years(int daysFromBirthday, bool expected)
    {
        var clock = new TestTimeProvider().CreateClock();
        var validator = new RegisterPatientDtoValidator(clock);
        var result = validator.Validate(new RegisterPatientDto
        {
            Email = "patient@example.test", Password = "Strong2026!", FirstName = "Jan", LastName = "Testowy",
            DateOfBirth = clock.Today.AddYears(-18).AddDays(daysFromBirthday)
        });
        Assert.Equal(expected, result.IsValid);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void ReviewRating_RejectsValuesOutsideOneToFive(int rating, bool expected)
    {
        var result = new CreateReviewDtoValidator().Validate(new CreateReviewDto { DoctorId = 1, Rating = rating });
        Assert.Equal(expected, result.IsValid);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public void NoteContent_EnforcesLengthBoundaries(int length, bool expected)
    {
        var result = new CreateNoteDtoValidator().Validate(new CreateNoteDto { AppointmentId = 1, Content = new string('a', length) });
        Assert.Equal(expected, result.IsValid);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(365, true)]
    [InlineData(366, false)]
    public void PrescriptionValidity_EnforcesOneTo365Days(int days, bool expected)
    {
        var result = new CreatePrescriptionDtoValidator().Validate(new CreatePrescriptionDto
        {
            AppointmentId = 1, Medication = "Example", Dosage = "Once daily", ValidityDays = days
        });
        Assert.Equal(expected, result.IsValid);
    }
}
