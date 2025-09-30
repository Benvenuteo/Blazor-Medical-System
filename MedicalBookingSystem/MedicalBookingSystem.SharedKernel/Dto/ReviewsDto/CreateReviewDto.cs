using System.ComponentModel.DataAnnotations;

namespace MedicalBookingSystem.SharedKernel.Dto.ReviewsDto
{
    public class CreateReviewDto
    {
        [Range(1, 5)]
        public int Rating { get; set; }

        [StringLength(2000)]
        public string Comment { get; set; }

        [Required]
        public int DoctorId { get; set; }
    }
}
