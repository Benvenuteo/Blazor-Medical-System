using AutoMapper;
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
        private readonly IMapper _mapper;

        public MedicalRecordService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _uow = unitOfWork;
            _mapper = mapper;
        }
        public async Task<Result<NoteDto>> AddNoteToAppointmentAsync(CreateNoteDto dto, int doctorId)
        {
            try
            {
                var appointment = await _uow.Appointments.GetByIdAsync(dto.AppointmentId);
                if (appointment == null)
                    return Result<NoteDto>.Failure<NoteDto>("Wizyta nie istnieje.");

                if (appointment.Note != null)
                    return Result<NoteDto>.Failure<NoteDto>("Notatka została już dodana.");

                var note = new Note
                {
                    AppointmentId = dto.AppointmentId,
                    Content = dto.Content,
                    CreatedDate = DateTime.UtcNow
                };

                await _uow.Notes.AddAsync(note);
                await _uow.SaveChangesAsync();

                var noteDto = _mapper.Map<NoteDto>(note);
                return Result<NoteDto>.Success(noteDto);
            }
            catch (Exception)
            {
                return Result<NoteDto>.Failure<NoteDto>("Wystąpił błąd podczas dodawania notatki.");
            }
        }

        public async Task<Result<PrescriptionDto>> CreatePrescriptionAsync(CreatePrescriptionDto dto, int doctorId)
        {
            try
            {
                var appointment = await _uow.Appointments.GetByIdAsync(dto.AppointmentId);
                if (appointment == null)
                    return Result<PrescriptionDto>.Failure<PrescriptionDto>("Wizyta nie istnieje.");

                var prescription = new Prescription
                {
                    AppointmentId = dto.AppointmentId,
                    Medication = dto.Medication,
                    Dosage = dto.Dosage,
                    Instructions = dto.Instructions,
                    CreatedDate = DateTime.Now,
                    ExpiryDate = DateTime.Today.AddDays(dto.ValidityDays)
                };

                await _uow.Prescriptions.AddAsync(prescription);
                await _uow.SaveChangesAsync();

                var prescriptionDto = _mapper.Map<PrescriptionDto>(prescription);
                return Result<PrescriptionDto>.Success(prescriptionDto);
            }
            catch (Exception)
            {
                return Result<PrescriptionDto>.Failure<PrescriptionDto>("Wystąpił błąd podczas wystawiania recepty.");
            }
        }

        public async Task<Result<IEnumerable<MedicalDocumentDto>>> GetPatientDocumentsAsync(int patientId)
        {
            var prescriptions = await _uow.Prescriptions.GetByPatientIdAsync(patientId);
            var notes = await _uow.Notes.GetByPatientIdAsync(patientId);

            // Mapowanie do klas pochodnych
            var prescriptionDtos = _mapper.Map<IEnumerable<PrescriptionDto>>(prescriptions);
            var noteDtos = _mapper.Map<IEnumerable<NoteDto>>(notes);

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

            var dto = _mapper.Map<PrescriptionDto>(prescription);
            return Result<PrescriptionDto>.Success(dto);
        }

        public async Task<Result<NoteDto>> GetNoteAsync(int id)
        {
            var note = await _uow.Notes.GetByIdAsync(id);
            if (note == null)
                return Result<NoteDto>.Failure<NoteDto>("Nie znaleziono notatki.");

            var dto = _mapper.Map<NoteDto>(note);
            return Result<NoteDto>.Success(dto);
        }
    }
}
