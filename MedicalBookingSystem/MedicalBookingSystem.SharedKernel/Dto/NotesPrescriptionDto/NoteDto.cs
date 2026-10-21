namespace MedicalBookingSystem.SharedKernel.Dto.NotesPrescriptionDto
{
    public class NoteDto : MedicalDocumentDto
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;

        public NoteDto()
        {
            DocumentType = "note";
        }
    }

}
