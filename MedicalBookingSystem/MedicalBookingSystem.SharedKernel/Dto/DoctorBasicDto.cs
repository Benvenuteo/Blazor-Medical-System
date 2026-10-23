namespace MedicalBookingSystem.SharedKernel.Dto
{
    public class DoctorBasicDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public List<string> Specializations { get; set; } = [];
        public decimal AverageRating { get; set; }
    }

}
