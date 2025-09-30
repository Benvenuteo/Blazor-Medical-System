namespace MedicalBookingSystem.SharedKernel.Dto.NotesPrescriptionDto
{
    public class PrescriptionDto : MedicalDocumentDto
    {
        public int Id { get; set; }
        public string Medication { get; set; }
        public string Dosage { get; set; }
        public string Instructions { get; set; }
        public DateTime ExpiryDate { get; set; }
        public PrescriptionDto()
        {
            DocumentType = "prescription";
        }
    }
}
