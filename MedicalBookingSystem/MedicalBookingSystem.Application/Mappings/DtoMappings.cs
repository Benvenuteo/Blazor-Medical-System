using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;
using MedicalBookingSystem.SharedKernel.Dto.NotesPrescriptionDto;
using MedicalBookingSystem.SharedKernel.Dto.ReviewsDto;

namespace MedicalBookingSystem.Application.Mappings;

/// <summary>Explicit, side-effect-free mappings between persisted entities and public contracts.</summary>
public static class DtoMappings
{
    public static PatientDto ToDto(this Patient patient) => new()
    {
        Id = patient.Id,
        FirstName = patient.FirstName,
        LastName = patient.LastName,
        Email = patient.Email,
        PhoneNumber = patient.PhoneNumber,
        DateOfBirth = patient.DateOfBirth
    };

    public static PatientBasicDto ToBasicDto(this Patient patient) => new()
    {
        Id = patient.Id,
        FirstName = patient.FirstName,
        LastName = patient.LastName
    };

    public static DoctorDto ToDto(this Doctor doctor) => new()
    {
        Id = doctor.Id,
        FirstName = doctor.FirstName,
        LastName = doctor.LastName,
        LicenseNumber = doctor.LicenseNumber,
        Region = doctor.Region,
        Bio = doctor.Bio,
        ImageUrl = doctor.ImageUrl,
        AverageRating = doctor.Reviews.Count == 0 ? 0 : (decimal)doctor.Reviews.Average(review => review.Rating),
        ReviewCount = doctor.Reviews.Count
    };

    public static DoctorDetailsDto ToDetailsDto(this Doctor doctor)
    {
        var summary = doctor.ToDto();
        return new DoctorDetailsDto
        {
            Id = summary.Id,
            FirstName = summary.FirstName,
            LastName = summary.LastName,
            LicenseNumber = summary.LicenseNumber,
            Region = summary.Region,
            Bio = summary.Bio,
            ImageUrl = summary.ImageUrl,
            AverageRating = summary.AverageRating,
            ReviewCount = summary.ReviewCount,
            Specializations = doctor.DoctorSpecializations.Select(item => item.Specialization.ToDto()).ToList(),
            Schedules = doctor.Schedules.Select(schedule => schedule.ToDto()).ToList()
        };
    }

    public static SpecializationDto ToDto(this Specialization specialization) => new()
    {
        Id = specialization.Id,
        Name = specialization.Name,
        Description = specialization.Description
    };

    public static DoctorScheduleDto ToDto(this DoctorSchedule schedule) => new()
    {
        Id = schedule.Id,
        DoctorId = schedule.DoctorId,
        StartTime = schedule.StartTime,
        EndTime = schedule.EndTime,
        IsAvailable = schedule.IsAvailable
    };

    public static AppointmentDto ToDto(this Appointment appointment) => new()
    {
        Id = appointment.Id,
        PatientId = appointment.PatientId,
        HasNote = appointment.Note is not null,
        HasPrescription = appointment.Prescription is not null,
        DoctorId = appointment.DoctorId,
        Date = appointment.Date,
        Status = appointment.Status.ToString(),
        DoctorName = appointment.Doctor is null ? string.Empty : $"{appointment.Doctor.FirstName} {appointment.Doctor.LastName}"
    };

    public static AppointmentDetailsDto ToDetailsDto(this Appointment appointment) => new()
    {
        Id = appointment.Id,
        HasNote = appointment.Note is not null,
        HasPrescription = appointment.Prescription is not null,
        PatientId = appointment.PatientId,
        DoctorId = appointment.DoctorId,
        Date = appointment.Date,
        Status = appointment.Status.ToString(),
        DoctorName = appointment.ToDto().DoctorName,
        Doctor = appointment.Doctor?.ToDto(),
        Patient = appointment.Patient?.ToBasicDto(),
        Note = appointment.Note?.ToDto(),
        Prescription = appointment.Prescription?.ToDto()
    };

    public static ReviewDto ToDto(this Review review) => new()
    {
        Id = review.Id,
        DoctorId = review.DoctorId,
        PatientId = review.PatientId,
        Rating = review.Rating,
        Comment = review.Comment
    };

    public static NoteDto ToDto(this Note note) => new()
    {
        Id = note.Id,
        AppointmentId = note.AppointmentId,
        Content = note.Content,
        CreatedDate = note.CreatedDate
    };

    public static PrescriptionDto ToDto(this Prescription prescription) => new()
    {
        Id = prescription.Id,
        AppointmentId = prescription.AppointmentId,
        Medication = prescription.Medication,
        Dosage = prescription.Dosage,
        Instructions = prescription.Instructions,
        CreatedDate = prescription.CreatedDate,
        ExpiryDate = prescription.ExpiryDate
    };
}
