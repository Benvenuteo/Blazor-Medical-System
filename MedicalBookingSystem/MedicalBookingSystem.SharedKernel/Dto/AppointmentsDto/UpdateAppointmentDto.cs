using MedicalBookingSystem.Domain.Models;

namespace MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto
{
    public class UpdateAppointmentDto
    {
        public DateTime NewDate { get; set; }
        public AppointmentStatus? Status { get; set; }
    }
}
