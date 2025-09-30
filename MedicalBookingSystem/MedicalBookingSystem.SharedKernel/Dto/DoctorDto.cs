namespace MedicalBookingSystem.SharedKernel.Dto
{
    public class DoctorDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string LicenseNumber { get; set; }
        public string Region { get; set; }
        public string Bio { get; set; }
        public decimal AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public string ImageUrl { get; set; }
    }

}
