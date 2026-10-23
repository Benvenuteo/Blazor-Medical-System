using FluentValidation;
using MedicalBookingSystem.Application.Queries;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Exceptions;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto.Accounts;
using MedicalBookingSystem.SharedKernel.Security;
using Microsoft.AspNetCore.Identity;

namespace MedicalBookingSystem.Application.Services;

public sealed class StaffAccountService(IUnitOfWorkFactory units, UserAccess access, IStaffAccountQueries queries,
    IPasswordHasher<StaffAccount> hasher, IValidator<CreateStaffAccountDto> createValidator,
    IValidator<UpdateStaffAccountDto> updateValidator, IValidator<ResetStaffPasswordDto> resetValidator,
    IValidator<AccountSearchDto> searchValidator)
{
    public async Task<Result<PagedResult<StaffAccountDto>>> SearchAsync(AccountSearchDto search)
    {
        await access.RequireAdminAsync(); var actorId = await access.StaffAccountIdAsync();
        var validation = await searchValidator.ValidateAsync(search);
        return !validation.IsValid ? Result.Failure<PagedResult<StaffAccountDto>>(Errors(validation)) : Result.Success(await queries.SearchAsync(search, actorId));
    }

    public async Task<Result<PagedResult<StaffDoctorOptionDto>>> UnassignedDoctorsAsync(AccountSearchDto search)
    {
        await access.RequireAdminAsync();
        var validation = await searchValidator.ValidateAsync(search);
        return !validation.IsValid ? Result.Failure<PagedResult<StaffDoctorOptionDto>>(Errors(validation)) : Result.Success(await queries.UnassignedDoctorsAsync(search));
    }

    public async Task<Result<StaffAccountDto>> GetAsync(int id)
    {
        await access.RequireAdminAsync(); var actorId = await access.StaffAccountIdAsync();
        using var unit = units.Create(); var account = await unit.StaffAccounts.GetByIdAsync(id);
        return account is null ? Result.Failure<StaffAccountDto>("Konto nie istnieje.", ErrorType.NotFound) : Result.Success(ToDto(account, actorId));
    }

    public async Task<Result<StaffAccountDto>> CreateAsync(CreateStaffAccountDto dto)
    {
        await access.RequireAdminAsync(); var actorId = await access.StaffAccountIdAsync();
        var validation = await createValidator.ValidateAsync(dto);
        if (!validation.IsValid) return Result.Failure<StaffAccountDto>(Errors(validation));
        using var unit = units.Create(); var email = dto.Email.Trim().ToLowerInvariant();
        if (await unit.StaffAccounts.AnyAsync(a => a.Email == email || (dto.DoctorId.HasValue && a.DoctorId == dto.DoctorId)))
            return Result.Failure<StaffAccountDto>("Email lub przypisanie lekarza są już używane.", ErrorType.Conflict);
        if (dto.DoctorId is int doctorId && !await unit.Doctors.AnyAsync(d => d.Id == doctorId))
            return Result.Failure<StaffAccountDto>("Lekarz nie istnieje.", ErrorType.NotFound);
        var account = new StaffAccount { Email = email, DisplayName = dto.DisplayName.Trim(),
            Role = dto.Role == Roles.Admin ? StaffRole.Admin : StaffRole.Doctor, DoctorId = dto.DoctorId, MustChangePassword = true };
        account.PasswordHash = hasher.HashPassword(account, dto.InitialPassword);
        await unit.StaffAccounts.AddAsync(account);
        var saved = await SaveAsync(unit);
        return saved.IsSuccess ? Result.Success(ToDto(account, actorId)) : Result.Failure<StaffAccountDto>(saved.Error!, saved.ErrorType);
    }

    public async Task<Result<StaffAccountDto>> UpdateAsync(int id, UpdateStaffAccountDto dto)
    {
        await access.RequireAdminAsync(); var actorId = await access.StaffAccountIdAsync();
        var validation = await updateValidator.ValidateAsync(dto);
        if (!validation.IsValid) return Result.Failure<StaffAccountDto>(Errors(validation));
        using var unit = units.Create(); var account = await unit.StaffAccounts.GetByIdAsync(id);
        if (account is null) return Result.Failure<StaffAccountDto>("Konto nie istnieje.", ErrorType.NotFound);
        if (account.Version != dto.Version) return Result.Failure<StaffAccountDto>("Konto zmieniło się. Odśwież dane.", ErrorType.Conflict);
        var email = dto.Email.Trim().ToLowerInvariant();
        if (actorId == id && (email != account.Email || dto.IsActive != account.IsActive))
            return Result.Failure<StaffAccountDto>("W tym panelu nie można zmienić własnego emaila ani dezaktywować własnego konta.", ErrorType.Conflict);
        if (await unit.StaffAccounts.AnyAsync(a => a.Id != id && a.Email == email))
            return Result.Failure<StaffAccountDto>("Email jest już używany.", ErrorType.Conflict);
        if (account.Email != email || account.IsActive != dto.IsActive) account.SecurityStamp = Guid.NewGuid();
        account.Email = email; account.DisplayName = dto.DisplayName.Trim(); account.IsActive = dto.IsActive;
        var saved = await SaveAsync(unit);
        return saved.IsSuccess ? Result.Success(ToDto(account, actorId)) : Result.Failure<StaffAccountDto>(saved.Error!, saved.ErrorType);
    }

    public async Task<Result> ResetPasswordAsync(int id, ResetStaffPasswordDto dto)
    {
        await access.RequireAdminAsync(); var actorId = await access.StaffAccountIdAsync();
        if (actorId == id) return Result.Failure("Użyj formularza zmiany własnego hasła.", ErrorType.Conflict);
        var validation = await resetValidator.ValidateAsync(dto);
        if (!validation.IsValid) return Result.Failure(Errors(validation));
        using var unit = units.Create(); var account = await unit.StaffAccounts.GetByIdAsync(id);
        if (account is null) return Result.Failure("Konto nie istnieje.", ErrorType.NotFound);
        if (account.Version != dto.Version) return Result.Failure("Konto zmieniło się. Odśwież dane.", ErrorType.Conflict);
        account.PasswordHash = hasher.HashPassword(account, dto.NewPassword);
        account.MustChangePassword = true; account.SecurityStamp = Guid.NewGuid();
        return await SaveAsync(unit);
    }

    private static async Task<Result> SaveAsync(IUnitOfWork unit)
    {
        try { await unit.SaveChangesAsync(); return Result.Success(); }
        catch (PersistenceConflictException exception)
        {
            return Result.Failure(exception.Kind == PersistenceConflictKind.LastAdministrator
                ? "Nie można wyłączyć ostatniego aktywnego administratora."
                : "Konto, email lub przypisanie lekarza zmieniły się w innym żądaniu. Odśwież dane.", ErrorType.Conflict);
        }
    }

    private static StaffAccountDto ToDto(StaffAccount account, int actorId) => new()
    {
        Id = account.Id, Email = account.Email, DisplayName = account.DisplayName, Role = account.Role.ToString(),
        DoctorId = account.DoctorId, IsActive = account.IsActive, MustChangePassword = account.MustChangePassword,
        Version = account.Version, IsCurrentUser = account.Id == actorId
    };
    private static string Errors(FluentValidation.Results.ValidationResult result) => string.Join("; ", result.Errors.Select(e => e.ErrorMessage));
}
