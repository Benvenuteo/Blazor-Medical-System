using AutoMapper;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;
using MedicalBookingSystem.SharedKernel.Dto.NotesPrescriptionDto;
using MedicalBookingSystem.SharedKernel.Dto.ReviewsDto;

namespace MedicalBookingSystem.Application.Mappings
{
    public class MedicalBookingSystemMappingProfile : Profile
    {

        public MedicalBookingSystemMappingProfile()
        {
            // Patient
            CreateMap<Patient, PatientDto>();
            CreateMap<Patient, PatientBasicDto>();
            CreateMap<RegisterPatientDto, Patient>()
                .ForMember(dest => dest.PasswordHash, opt => opt.Ignore());

            // Doctor
            CreateMap<Doctor, DoctorDto>()
                .ForMember(dest => dest.AverageRating,
                    opt => opt.MapFrom(src => src.Reviews.Any() ?
                        src.Reviews.Average(r => r.Rating) : 0))
                .ForMember(dest => dest.ReviewCount,
                    opt => opt.MapFrom(src => src.Reviews.Count));

            CreateMap<Doctor, DoctorDetailsDto>()
                .IncludeBase<Doctor, DoctorDto>()
                .ForMember(dest => dest.Specializations,
                    opt => opt.MapFrom(src => src.DoctorSpecializations
                        .Select(ds => ds.Specialization)
                        .ToList()))
                .ForMember(dest => dest.Schedules,
                    opt => opt.MapFrom(src => src.Schedules.ToList()));

            CreateMap<Doctor, DoctorBasicDto>()
                .ForMember(dest => dest.Specializations,
                    opt => opt.MapFrom(src => GetFirstSpecializationName(src)));


            // Specialization
            CreateMap<Specialization, SpecializationDto>();

            // Appointment
            CreateMap<Appointment, AppointmentDto>()
                .ForMember(dest => dest.Status,
                    opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.DoctorName,
                    opt => opt.MapFrom(src => src.Doctor.FirstName + " " + src.Doctor.LastName));

            CreateMap<Appointment, AppointmentDetailsDto>()
                .IncludeBase<Appointment, AppointmentDto>()
                .ForMember(dest => dest.Note,
                    opt => opt.MapFrom(src => src.Note))
                .ForMember(dest => dest.Prescription,
                    opt => opt.MapFrom(src => src.Prescription))
                .ForMember(dest => dest.Patient,
                    opt => opt.MapFrom(src => src.Patient))
                .ForMember(dest => dest.Doctor,
                    opt => opt.MapFrom(src => src.Doctor));

            CreateMap<CreateAppointmentDto, Appointment>()
                .ForMember(dest => dest.Status,
                    opt => opt.MapFrom(_ => AppointmentStatus.Scheduled));

            CreateMap<UpdateAppointmentDto, Appointment>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) =>
                    srcMember != null)); // Aktualizuj tylko podane pola

            // Review
            CreateMap<Review, ReviewDto>();

            CreateMap<CreateReviewDto, Review>()
                .ForMember(dest => dest.CreatedDate,
                    opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.PatientId,
                    opt => opt.Ignore()); // Ustawiane w serwisie


            CreateMap<CreatePrescriptionDto, Prescription>()
                .ForMember(dest => dest.CreatedDate,
                    opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.ExpiryDate,
                    opt => opt.MapFrom(src => DateTime.UtcNow.AddDays(src.ValidityDays)));

            // Note
            CreateMap<Note, NoteDto>();

            // DoctorSchedule
            CreateMap<DoctorSchedule, DoctorScheduleDto>();


            CreateMap<Note, NoteDto>();
            CreateMap<Prescription, PrescriptionDto>();

        }

        private static string GetFirstSpecializationName(Doctor doctor)
        {
            if (doctor.DoctorSpecializations == null || !doctor.DoctorSpecializations.Any())
                return "Brak specjalizacji";

            return doctor.DoctorSpecializations.First().Specialization?.Name ?? "Brak specjalizacji";
        }
    }
}
