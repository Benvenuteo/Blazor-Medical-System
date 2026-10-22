using MedicalBookingSystem.Application.Queries;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto.Accounts;
using Microsoft.EntityFrameworkCore;

namespace MedicalBookingSystem.Infrastructure.Queries;

public sealed class StaffAccountQueries(IDbContextFactory<ApplicationDbContext> contexts) : IStaffAccountQueries
{
    public async Task<PagedResult<StaffAccountDto>> SearchAsync(AccountSearchDto search, int currentStaffId)
    {
        using var context = contexts.CreateDbContext();
        var query = context.StaffAccounts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var text = search.Search.Trim(); var email = text.ToLowerInvariant();
            query = query.Where(a => a.Email.Contains(email) || a.DisplayName.Contains(text));
        }
        var count = await query.CountAsync();
        var items = await query.OrderBy(a => a.Email).ThenBy(a => a.Id).Skip((search.Page - 1) * search.PageSize).Take(search.PageSize)
            .Select(a => new StaffAccountDto
            {
                Id = a.Id, Email = a.Email, DisplayName = a.DisplayName, Role = a.Role.ToString(), IsActive = a.IsActive,
                MustChangePassword = a.MustChangePassword, DoctorId = a.DoctorId,
                DoctorName = a.Doctor == null ? null : a.Doctor.FirstName + " " + a.Doctor.LastName,
                Version = a.Version, IsCurrentUser = a.Id == currentStaffId
            }).ToListAsync();
        return new(items, count, search.Page, search.PageSize);
    }

    public async Task<PagedResult<StaffDoctorOptionDto>> UnassignedDoctorsAsync(AccountSearchDto search)
    {
        using var context = contexts.CreateDbContext();
        var query = context.Doctors.AsNoTracking().Where(d => !context.StaffAccounts.Any(a => a.DoctorId == d.Id));
        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var text = search.Search.Trim();
            query = query.Where(d => d.FirstName.Contains(text) || d.LastName.Contains(text) || d.LicenseNumber.Contains(text));
        }
        var count = await query.CountAsync();
        var items = await query.OrderBy(d => d.LastName).ThenBy(d => d.Id).Skip((search.Page - 1) * search.PageSize).Take(search.PageSize)
            .Select(d => new StaffDoctorOptionDto(d.Id, d.FirstName + " " + d.LastName, d.LicenseNumber)).ToListAsync();
        return new(items, count, search.Page, search.PageSize);
    }
}
