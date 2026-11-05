using FluentValidation;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Exceptions;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto;

namespace MedicalBookingSystem.Application.Services;

public interface IGeocodingService
{
    /// <returns>The best match, or null when nothing matches or the provider is unavailable.</returns>
    Task<GeoPointDto?> GeocodeAsync(string query, CancellationToken cancellationToken = default);
}

/// <summary>Clinics and the doctors who work there. Administrators manage them; anyone signed in can geocode an address.</summary>
public sealed class ClinicService(IUnitOfWorkFactory units, UserAccess access, IGeocodingService geocoding, IValidator<SaveClinicDto> validator)
{
    public async Task<Result<IReadOnlyList<ClinicDto>>> ListAsync()
    {
        await access.RequireAdminAsync();
        using var unit = units.Create();
        var clinics = (await unit.Clinics.GetAllAsync()).OrderBy(c => c.City).ThenBy(c => c.Name).ToList();
        var links = (await unit.DoctorClinics.GetAllAsync()).ToLookup(l => l.ClinicId, l => l.DoctorId);
        IReadOnlyList<ClinicDto> result = clinics.Select(c => ToDto(c, links[c.Id])).ToList();
        return Result.Success(result);
    }

    /// <summary>Clinics a doctor works at, for the schedule form (admin or that doctor).</summary>
    public async Task<Result<IReadOnlyList<ClinicDto>>> ForDoctorAsync(int doctorId)
    {
        await access.RequireScheduleManagementAsync(doctorId);
        using var unit = units.Create();
        var ids = (await unit.DoctorClinics.FindAsync(l => l.DoctorId == doctorId)).Select(l => l.ClinicId).ToHashSet();
        IReadOnlyList<ClinicDto> result = (await unit.Clinics.FindAsync(c => ids.Contains(c.Id)))
            .OrderBy(c => c.Name).Select(c => ToDto(c, [doctorId])).ToList();
        return Result.Success(result);
    }

    public async Task<Result<ClinicDto>> CreateAsync(SaveClinicDto dto)
    {
        await access.RequireAdminAsync();
        var validation = await validator.ValidateAsync(dto);
        if (!validation.IsValid) return Result.Failure<ClinicDto>(Errors(validation));
        using var unit = units.Create();
        var invalid = await UnknownDoctorAsync(unit, dto.DoctorIds);
        if (invalid is not null) return Result.Failure<ClinicDto>(invalid, ErrorType.NotFound);
        var clinic = new Clinic();
        Apply(clinic, dto);
        await unit.Clinics.AddAsync(clinic);
        foreach (var doctorId in dto.DoctorIds.Distinct())
            await unit.DoctorClinics.AddAsync(new DoctorClinic { Clinic = clinic, DoctorId = doctorId });
        var saved = await SaveAsync(unit);
        return saved.IsSuccess ? Result.Success(ToDto(clinic, dto.DoctorIds.Distinct())) : Result.Failure<ClinicDto>(saved.Error!, saved.ErrorType);
    }

    public async Task<Result<ClinicDto>> UpdateAsync(int id, SaveClinicDto dto)
    {
        await access.RequireAdminAsync();
        var validation = await validator.ValidateAsync(dto);
        if (!validation.IsValid) return Result.Failure<ClinicDto>(Errors(validation));
        using var unit = units.Create();
        var clinic = await unit.Clinics.GetByIdAsync(id);
        if (clinic is null) return Result.Failure<ClinicDto>("Placówka nie istnieje.", ErrorType.NotFound);
        var invalid = await UnknownDoctorAsync(unit, dto.DoctorIds);
        if (invalid is not null) return Result.Failure<ClinicDto>(invalid, ErrorType.NotFound);
        var wanted = dto.DoctorIds.ToHashSet();
        var current = (await unit.DoctorClinics.FindAsync(l => l.ClinicId == id)).ToList();
        foreach (var removed in current.Where(l => !wanted.Contains(l.DoctorId)))
        {
            // A doctor who still has shifts here cannot be detached, otherwise those shifts would point to a clinic they do not work at.
            if (await unit.DoctorSchedules.AnyInClinicAsync(id, removed.DoctorId))
                return Result.Failure<ClinicDto>("Lekarz ma w tej placówce grafik. Najpierw przenieś lub usuń jego dyżury.", ErrorType.Conflict);
            unit.DoctorClinics.Remove(removed);
        }
        foreach (var doctorId in wanted.Where(d => current.All(l => l.DoctorId != d)))
            await unit.DoctorClinics.AddAsync(new DoctorClinic { ClinicId = id, DoctorId = doctorId });
        Apply(clinic, dto);
        var saved = await SaveAsync(unit);
        return saved.IsSuccess ? Result.Success(ToDto(clinic, wanted)) : Result.Failure<ClinicDto>(saved.Error!, saved.ErrorType);
    }

    public async Task<Result> DeleteAsync(int id)
    {
        await access.RequireAdminAsync();
        using var unit = units.Create();
        var clinic = await unit.Clinics.GetByIdAsync(id);
        if (clinic is null) return Result.Failure("Placówka nie istnieje.", ErrorType.NotFound);
        if (await unit.Appointments.AnyAsync(a => a.ClinicId == id))
            return Result.Failure("Placówka ma zapisane wizyty i musi zostać w historii.", ErrorType.Conflict);
        if (await unit.DoctorSchedules.AnyInClinicAsync(id))
            return Result.Failure("Placówka ma grafiki lekarzy. Najpierw je usuń lub przenieś.", ErrorType.Conflict);
        unit.Clinics.Remove(clinic);
        return await SaveAsync(unit);
    }

    /// <summary>Address search for any signed-in user: administrators placing a clinic, patients choosing a search centre.</summary>
    public async Task<Result<GeoPointDto>> GeocodeAsync(string? query, CancellationToken cancellationToken = default)
    {
        await access.IdentityAsync();
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length is < 3 or > 200)
            return Result.Failure<GeoPointDto>("Wpisz adres lub miejscowość (3–200 znaków).");
        var point = await geocoding.GeocodeAsync(query.Trim(), cancellationToken);
        return point is null
            ? Result.Failure<GeoPointDto>("Nie znaleziono takiego miejsca. Spróbuj dodać miasto lub kod pocztowy.", ErrorType.NotFound)
            : Result.Success(point);
    }

    private static async Task<string?> UnknownDoctorAsync(IUnitOfWork unit, IEnumerable<int> doctorIds)
    {
        foreach (var id in doctorIds.Distinct())
            if (!await unit.Doctors.AnyAsync(d => d.Id == id)) return $"Lekarz {id} nie istnieje.";
        return null;
    }

    private static void Apply(Clinic clinic, SaveClinicDto dto)
    {
        clinic.Name = dto.Name.Trim();
        clinic.Street = dto.Street.Trim();
        clinic.City = dto.City.Trim();
        clinic.PostalCode = dto.PostalCode.Trim();
        clinic.Phone = dto.Phone.Trim();
        clinic.Latitude = Math.Round(dto.Latitude!.Value, 6);
        clinic.Longitude = Math.Round(dto.Longitude!.Value, 6);
    }

    private static ClinicDto ToDto(Clinic clinic, IEnumerable<int> doctorIds) => new()
    {
        Id = clinic.Id, Name = clinic.Name, Street = clinic.Street, City = clinic.City, PostalCode = clinic.PostalCode,
        Phone = clinic.Phone, Latitude = clinic.Latitude, Longitude = clinic.Longitude, DoctorIds = doctorIds.Order().ToList()
    };

    private static async Task<Result> SaveAsync(IUnitOfWork unit)
    {
        try { await unit.SaveChangesAsync(); return Result.Success(); }
        catch (PersistenceConflictException) { return Result.Failure("Placówka o tej nazwie już istnieje albo dane zmieniły się w międzyczasie.", ErrorType.Conflict); }
    }

    private static string Errors(FluentValidation.Results.ValidationResult result) => string.Join("; ", result.Errors.Select(e => e.ErrorMessage));
}
