using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;

namespace MedicalBookingSystem.Infrastructure.Repositories;

public sealed class DoctorRepository(ApplicationDbContext context) : Repository<Doctor>(context), IDoctorRepository;
