namespace MedicalBookingSystem.SharedKernel.Dto.NotesPrescriptionDto
{
    public class PrescriptionDto : MedicalDocumentDto
    {
        public int Id { get; set; }
        public string Medication { get; set; } = string.Empty;
        public string Dosage { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public PrescriptionDto()
        {
            DocumentType = "prescription";
        }
    }
}
