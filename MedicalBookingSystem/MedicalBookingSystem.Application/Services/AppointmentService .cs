using AutoMapper;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;

namespace MedicalBookingSystem.Application.Services
{
    public class AppointmentService : IAppointmentService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;

        public AppointmentService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _uow = unitOfWork;
            _mapper = mapper;
        }

        public async Task<Result> CancelAppointmentAsync(int id, int patientId)
        {
            try
            {
                var appointment = await _uow.Appointments
                   .GetByIdAsync(id);

                if (appointment == null)
                    return Result.Failure("Nie znaleziono wizyty");

                if (appointment.PatientId != patientId)
                    return Result.Failure("Wizyta nie należy do danego pacjenta");

                if (appointment.Status == AppointmentStatus.Cancelled)
                    return Result.Failure("Wizyta już jest anulowana.");

                if (appointment.Date < DateTime.Now.AddHours(24))
                    return Result.Failure("Anulowanie niemożliwe mniej niż 24 godziny przed wizytą. Prosimy o kontakt z przychodnią");
                appointment.Status = AppointmentStatus.Cancelled;

                await _uow.SaveChangesAsync();
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Nie udało się anulować wizyty: {ex.Message}");
            }
        }

        public async Task<Result<AppointmentDto>> CreateAppointmentAsync(CreateAppointmentDto dto, int patientId)
        {
            var doctor = await _uow.Doctors.GetByIdAsync(dto.DoctorId);
            if (doctor == null)
                return Result<AppointmentDto>.Failure<AppointmentDto>("Lekarz nie istnieje");

            var isAvailable = await _uow.DoctorSchedules.IsTimeSlotAvailableAsync(dto.DoctorId, dto.Date);
            if (!isAvailable)
                return Result<AppointmentDto>.Failure<AppointmentDto>("Lekarz nie jest dostępny w wybranym terminie");

            var appointment = new Appointment
            {
                PatientId = patientId,
                DoctorId = dto.DoctorId,
                Date = dto.Date,
                Status = AppointmentStatus.Scheduled
            };

            await _uow.Appointments.AddAsync(appointment);
            await _uow.SaveChangesAsync();

            return Result<AppointmentDto>.Success(_mapper.Map<AppointmentDto>(appointment));
        }

        public async Task<Result<IEnumerable<AppointmentDto>>> GetPastAppointmentsAsync(int patientId)
        {
            try
            {
                var appointments = await _uow.Appointments.GetHistoryByPatientIdAsync(patientId);

                var mappedAppointments = _mapper.Map<IEnumerable<AppointmentDto>>(appointments);

                return Result.Success<IEnumerable<AppointmentDto>>(mappedAppointments);
            }
            catch (Exception ex)
            {
                return Result.Failure<IEnumerable<AppointmentDto>>($"Nie udało się załadować historii wizyt: {ex.Message}");
            }
        }

        public async Task<Result<IEnumerable<AppointmentDto>>> GetUpcomingAppointmentsAsync(int patientId)
        {
            try
            {
                var appointments = await _uow.Appointments.GetUpcomingByPatientIdAsync(patientId);

                var mappedAppointments = _mapper.Map<IEnumerable<AppointmentDto>>(appointments);

                return Result.Success<IEnumerable<AppointmentDto>>(mappedAppointments);
            }
            catch (Exception ex)
            {
                return Result.Failure<IEnumerable<AppointmentDto>>($"Nie udało się załadować przyszłych wizyt: {ex.Message}");
            }
        }

        public async Task<Result> UpdateAppointmentAsync(int id, UpdateAppointmentDto dto)
        {
            try
            {
                var appointment = await _uow.Appointments.GetByIdAsync(id);
                if (appointment == null)
                    return Result.Failure("Nie znaleziono wizyty.");

                bool isDoctorAvailable = await _uow.DoctorSchedules.IsTimeSlotAvailableAsync(appointment.DoctorId, dto.NewDate);

                if (!isDoctorAvailable)
                    return Result.Failure("The appointment conflicts with another appointment.");

                appointment.Date = dto.NewDate;

                await _uow.SaveChangesAsync();
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Failed to update appointment: {ex.Message}");
            }
        }

        public async Task<Result<AppointmentDetailsDto>> GetAppointmentDetailsAsync(int appointmentId, int patientId)
        {
            var appointment = await _uow.Appointments.GetAppointmentByIdAsync(appointmentId);

            if (appointment == null || appointment.PatientId != patientId)
                return Result<AppointmentDetailsDto>.Failure<AppointmentDetailsDto>("Nie znaleziono wizyty lub nie masz do niej dostępu.");

            var dto = _mapper.Map<AppointmentDetailsDto>(appointment);
            return Result<AppointmentDetailsDto>.Success(dto);
        }

        public List<AppointmentDto> GetNextAppointmentsByDoctorId(int doctorId)
        {
            var appointments = _uow.Appointments.GetNextAppointmentsByDoctorId(doctorId);

            if (appointments == null)
                return new List<AppointmentDto>();
            var filtered = appointments
                .Where(a => a.Note == null)
                .ToList();
            var mappedAppointments = _mapper.Map<IEnumerable<AppointmentDto>>(filtered);
            return mappedAppointments.ToList();
        }

        public List<AppointmentDto> GetPreviousAppointmentsByDoctorId(int doctorId)
        {
            var appointments = _uow.Appointments.GetPreviousAppointmentsByDoctorId(doctorId);

            if (appointments == null)
                return new List<AppointmentDto>();

            // Filtrowanie po braku recepty – zależnie od modelu
            var filtered = appointments
                .Where(a => a.Prescription == null)
                .ToList();

            var mappedAppointments = _mapper.Map<IEnumerable<AppointmentDto>>(filtered);
            return mappedAppointments.ToList();
        }
    }
}
