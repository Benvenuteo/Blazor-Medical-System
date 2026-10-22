using FluentValidation;
using MedicalBookingSystem.Domain.Exceptions;
using MedicalBookingSystem.Domain.Scheduling;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Application.Mappings;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Dto.NotesPrescriptionDto;

namespace MedicalBookingSystem.Application.Services
{
    public class MedicalRecordService : IMedicalRecordService
    {
        private readonly IUnitOfWorkFactory _units;
        private readonly UserAccess _access;
        private readonly ClinicClock _clock;
        private readonly IValidator<CreateNoteDto> _noteValidator;
        private readonly IValidator<CreatePrescriptionDto> _prescriptionValidator;


        public MedicalRecordService(IUnitOfWorkFactory units, ClinicClock clock, UserAccess access, IValidator<CreateNoteDto> noteValidator, IValidator<CreatePrescriptionDto> prescriptionValidator)
        {
            _units = units;
            _access = access;
            _clock = clock;
            _noteValidator = noteValidator;
            _prescriptionValidator = prescriptionValidator;

        }
        public async Task<Result<NoteDto>> AddNoteToAppointmentAsync(CreateNoteDto dto, int doctorId)
        {
            using var _uow = _units.Create();
            await _access.RequireDoctorAsync(doctorId);
            var appointment = await _uow.Appointments.GetByIdAsync(dto.AppointmentId);
            if (appointment is null) return Result.Failure<NoteDto>("Wizyta nie istnieje.", ErrorType.NotFound);
            await _access.RequireDoctorAsync(appointment.DoctorId);
            var normalized = new CreateNoteDto { AppointmentId = dto.AppointmentId, Content = dto.Content?.Trim() ?? string.Empty };
            var validation = await _noteValidator.ValidateAsync(normalized);
            if (!validation.IsValid) return Result.Failure<NoteDto>(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
            if (!CanDocument(appointment))
                return Result.Failure<NoteDto>("Dokumentację można dodać wyłącznie do odbytej wizyty.", ErrorType.Conflict);
            if (await _uow.Notes.AnyAsync(n => n.AppointmentId == dto.AppointmentId))
                return Result.Failure<NoteDto>("Notatka została już dodana.", ErrorType.Conflict);
            var note = new Note { AppointmentId = dto.AppointmentId, Content = normalized.Content, CreatedDate = _clock.UtcNow };
            await _uow.Notes.AddAsync(note);
            try { await _uow.SaveChangesAsync(); }
            catch (PersistenceConflictException)
            {
                return Result.Failure<NoteDto>("Notatka została już dodana w innym żądaniu.", ErrorType.Conflict);
            }
            return Result.Success(note.ToDto());
        }

        public async Task<Result<PrescriptionDto>> CreatePrescriptionAsync(CreatePrescriptionDto dto, int doctorId)
        {
            using var _uow = _units.Create();
            await _access.RequireDoctorAsync(doctorId);
            var appointment = await _uow.Appointments.GetByIdAsync(dto.AppointmentId);
            if (appointment is null) return Result.Failure<PrescriptionDto>("Wizyta nie istnieje.", ErrorType.NotFound);
            await _access.RequireDoctorAsync(appointment.DoctorId);
            var validation = await _prescriptionValidator.ValidateAsync(dto);
            if (!validation.IsValid) return Result.Failure<PrescriptionDto>(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
            if (!CanDocument(appointment))
                return Result.Failure<PrescriptionDto>("Receptę można wystawić wyłącznie do odbytej wizyty.", ErrorType.Conflict);
            if (await _uow.Prescriptions.AnyAsync(p => p.AppointmentId == dto.AppointmentId))
                return Result.Failure<PrescriptionDto>("Recepta została już dodana.", ErrorType.Conflict);
            var prescription = new Prescription
            {
                AppointmentId = dto.AppointmentId, Medication = dto.Medication.Trim(), Dosage = dto.Dosage.Trim(),
                Instructions = dto.Instructions ?? string.Empty, CreatedDate = _clock.UtcNow,
                ExpiryDate = _clock.Today.AddDays(dto.ValidityDays)
            };
            await _uow.Prescriptions.AddAsync(prescription);
            try { await _uow.SaveChangesAsync(); }
            catch (PersistenceConflictException)
            {
                return Result.Failure<PrescriptionDto>("Recepta została już dodana w innym żądaniu.", ErrorType.Conflict);
            }
            return Result.Success(prescription.ToDto());
        }

        private bool CanDocument(Appointment appointment) => appointment.Status == AppointmentStatus.Completed &&
            appointment.Date <= _clock.Now.Subtract(AppointmentSlots.Duration);

        public async Task<Result<IEnumerable<MedicalDocumentDto>>> GetPatientDocumentsAsync(int patientId)
        {
            using var _uow = _units.Create();
            await _access.RequirePatientAsync(patientId);
            var prescriptions = await _uow.Prescriptions.GetByPatientIdAsync(patientId);
            var notes = await _uow.Notes.GetByPatientIdAsync(patientId);

            // Mapowanie do klas pochodnych
            var prescriptionDtos = prescriptions.Select(item => item.ToDto()).ToList();
            var noteDtos = notes.Select(item => item.ToDto()).ToList();

            // Rzutowanie do typu bazowego
            var allDocs = prescriptionDtos
                .Cast<MedicalDocumentDto>()
                .Concat(noteDtos.Cast<MedicalDocumentDto>())
                .OrderByDescending(d => d.CreatedDate)
                .ToList();

            return Result.Success<IEnumerable<MedicalDocumentDto>>(allDocs);
        }


        public async Task<Result<PrescriptionDto>> GetPrescriptionAsync(int id)
        {
            using var _uow = _units.Create();
            var prescription = await _uow.Prescriptions.GetByIdAsync(id);
            if (prescription == null)
                return Result<PrescriptionDto>.Failure<PrescriptionDto>("Nie znaleziono recepty.", ErrorType.NotFound);

            var appointment = await _uow.Appointments.GetByIdAsync(prescription.AppointmentId);
            if (appointment is null) throw new AccessDeniedException(true);
            await _access.RequireMedicalRecordAsync(appointment.PatientId, appointment.DoctorId);
            var dto = prescription.ToDto();
            return Result<PrescriptionDto>.Success(dto);
        }

        public async Task<Result<NoteDto>> GetNoteAsync(int id)
        {
            using var _uow = _units.Create();
            var note = await _uow.Notes.GetByIdAsync(id);
            if (note == null)
                return Result<NoteDto>.Failure<NoteDto>("Nie znaleziono notatki.", ErrorType.NotFound);

            var appointment = await _uow.Appointments.GetByIdAsync(note.AppointmentId);
            if (appointment is null) throw new AccessDeniedException(true);
            await _access.RequireMedicalRecordAsync(appointment.PatientId, appointment.DoctorId);
            var dto = note.ToDto();
            return Result<NoteDto>.Success(dto);
        }
    }
}
