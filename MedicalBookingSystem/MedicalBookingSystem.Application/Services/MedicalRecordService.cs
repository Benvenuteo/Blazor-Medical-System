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
        private readonly IUnitOfWork _uow;
        private readonly UserAccess _access;
        private readonly ClinicClock _clock;


        public MedicalRecordService(IUnitOfWork unitOfWork, ClinicClock clock, UserAccess access)
        {
            _uow = unitOfWork;
            _access = access;
            _clock = clock;

        }
        public async Task<Result<NoteDto>> AddNoteToAppointmentAsync(CreateNoteDto dto, int doctorId)
        {
            await _access.RequireDoctorAsync(doctorId);
            try
            {
                var appointment = await _uow.Appointments.GetByIdAsync(dto.AppointmentId);
                if (appointment == null)
                    return Result<NoteDto>.Failure<NoteDto>("Wizyta nie istnieje.");

                await _access.RequireDoctorAsync(appointment.DoctorId);

                if (appointment.Note != null)
                    return Result<NoteDto>.Failure<NoteDto>("Notatka została już dodana.");

                var note = new Note
                {
                    AppointmentId = dto.AppointmentId,
                    Content = dto.Content,
                    CreatedDate = _clock.UtcNow
                };

                await _uow.Notes.AddAsync(note);
                await _uow.SaveChangesAsync();

                var noteDto = note.ToDto();
                return Result<NoteDto>.Success(noteDto);
            }
            catch (Exception ex) when (ex is not AccessDeniedException)
            {
                return Result<NoteDto>.Failure<NoteDto>("Wystąpił błąd podczas dodawania notatki.");
            }
        }

        public async Task<Result<PrescriptionDto>> CreatePrescriptionAsync(CreatePrescriptionDto dto, int doctorId)
        {
            await _access.RequireDoctorAsync(doctorId);
            try
            {
                var appointment = await _uow.Appointments.GetByIdAsync(dto.AppointmentId);
                if (appointment == null)
                    return Result<PrescriptionDto>.Failure<PrescriptionDto>("Wizyta nie istnieje.");

                await _access.RequireDoctorAsync(appointment.DoctorId);

                var prescription = new Prescription
                {
                    AppointmentId = dto.AppointmentId,
                    Medication = dto.Medication,
                    Dosage = dto.Dosage,
                    Instructions = dto.Instructions,
                    CreatedDate = _clock.UtcNow,
                    ExpiryDate = _clock.Today.AddDays(dto.ValidityDays)
                };

                await _uow.Prescriptions.AddAsync(prescription);
                await _uow.SaveChangesAsync();

                var prescriptionDto = prescription.ToDto();
                return Result<PrescriptionDto>.Success(prescriptionDto);
            }
            catch (Exception ex) when (ex is not AccessDeniedException)
            {
                return Result<PrescriptionDto>.Failure<PrescriptionDto>("Wystąpił błąd podczas wystawiania recepty.");
            }
        }

        public async Task<Result<IEnumerable<MedicalDocumentDto>>> GetPatientDocumentsAsync(int patientId)
        {
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
            var prescription = await _uow.Prescriptions.GetByIdAsync(id);
            if (prescription == null)
                return Result<PrescriptionDto>.Failure<PrescriptionDto>("Nie znaleziono recepty.");

            var appointment = await _uow.Appointments.GetByIdAsync(prescription.AppointmentId);
            if (appointment is null) throw new AccessDeniedException(true);
            await _access.RequireMedicalRecordAsync(appointment.PatientId, appointment.DoctorId);
            var dto = prescription.ToDto();
            return Result<PrescriptionDto>.Success(dto);
        }

        public async Task<Result<NoteDto>> GetNoteAsync(int id)
        {
            var note = await _uow.Notes.GetByIdAsync(id);
            if (note == null)
                return Result<NoteDto>.Failure<NoteDto>("Nie znaleziono notatki.");

            var appointment = await _uow.Appointments.GetByIdAsync(note.AppointmentId);
            if (appointment is null) throw new AccessDeniedException(true);
            await _access.RequireMedicalRecordAsync(appointment.PatientId, appointment.DoctorId);
            var dto = note.ToDto();
            return Result<NoteDto>.Success(dto);
        }
    }
}
