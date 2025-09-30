namespace MedicalBookingSystem.SharedKernel.Dto
{
    public class DoctorBasicDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public List<string> Specializations { get; set; }
        public decimal AverageRating { get; set; }
    }

}
