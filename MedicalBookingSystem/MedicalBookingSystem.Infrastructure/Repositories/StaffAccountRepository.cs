using MedicalBookingSystem.Domain.Models;

namespace MedicalBookingSystem.Infrastructure.Repositories;

public sealed class StaffAccountRepository(ApplicationDbContext context) : Repository<StaffAccount>(context);
