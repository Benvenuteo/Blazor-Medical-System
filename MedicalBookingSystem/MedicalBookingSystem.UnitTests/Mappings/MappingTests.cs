using System.Text.Json;
using MedicalBookingSystem.Application.Mappings;
using MedicalBookingSystem.Domain.Models;

namespace MedicalBookingSystem.UnitTests.Mappings;

public sealed class MappingTests
{
    [Fact]
    public void PatientMapping_DoesNotExposePasswordHash()
    {
        var patient = new Patient { Id = 1, PasswordHash = "private-hash", Email = "patient@example.test" };

        var json = JsonSerializer.Serialize(patient.ToDto());

        Assert.DoesNotContain("PasswordHash", json);
        Assert.DoesNotContain("private-hash", json);
    }

    [Fact]
    public void DoctorMapping_CalculatesRatingFromReviews()
    {
        var doctor = new Doctor { Reviews = [new Review { Rating = 2 }, new Review { Rating = 5 }] };

        var result = doctor.ToDto();

        Assert.Equal(3.5m, result.AverageRating);
        Assert.Equal(2, result.ReviewCount);
    }

    [Fact]
    public void Rating_WhenLastReviewIsRemoved_ResetsToZero()
    {
        var doctor = new Doctor { Reviews = [new Review { Rating = 5 }] };
        doctor.UpdateAverageRating();

        doctor.Reviews.Clear();
        doctor.UpdateAverageRating();

        Assert.Equal(0, doctor.AverageRating);
    }

    [Fact]
    public void AppointmentDetails_PreservesMedicalDocumentDiscriminators()
    {
        var appointment = new Appointment
        {
            Id = 7, Patient = new Patient { Id = 2 }, Doctor = new Doctor { Id = 3 },
            Note = new Note { AppointmentId = 7, Content = "Example note" },
            Prescription = new Prescription { AppointmentId = 7, Medication = "Example medication" }
        };

        var result = appointment.ToDetailsDto();

        Assert.Equal("note", result.Note!.DocumentType);
        Assert.Equal("prescription", result.Prescription!.DocumentType);
        Assert.Equal(2, result.Patient!.Id);
        Assert.Equal(7, result.Note.AppointmentId);
    }
}
