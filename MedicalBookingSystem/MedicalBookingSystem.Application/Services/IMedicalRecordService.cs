using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Dto.NotesPrescriptionDto;

namespace MedicalBookingSystem.Application.Services
{
    public interface IMedicalRecordService
    {
        Task<Result<PrescriptionDto>> CreatePrescriptionAsync(CreatePrescriptionDto dto, int doctorId);
        Task<Result<NoteDto>> AddNoteToAppointmentAsync(CreateNoteDto dto, int doctorId);
        Task<Result<IEnumerable<MedicalDocumentDto>>> GetPatientDocumentsAsync(int patientId);
        Task<Result<PrescriptionDto>> GetPrescriptionAsync(int id);
        Task<Result<NoteDto>> GetNoteAsync(int id);
    }
}
