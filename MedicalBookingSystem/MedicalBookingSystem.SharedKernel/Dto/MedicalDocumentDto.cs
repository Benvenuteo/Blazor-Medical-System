namespace MedicalBookingSystem.SharedKernel.Dto
{
    public class MedicalDocumentDto
    {
        public DateTime CreatedDate { get; set; }
        public int AppointmentId { get; set; }
        public string DocumentType { get; set; }
    }

}
