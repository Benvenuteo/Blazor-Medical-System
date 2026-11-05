using MedicalBookingSystem.Application.Notifications;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Exceptions;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.Domain.Scheduling;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto;
using Microsoft.Extensions.Options;

namespace MedicalBookingSystem.Application.Services;

public interface IWaitlistNotifier
{
    /// <summary>A slot starting at <paramref name="freedStart"/> became available for this doctor.</summary>
    Task SlotFreedAsync(int doctorId, DateTime freedStart);
}

/// <summary>
/// Patients wait for an earlier slot with a chosen doctor. When a visit is cancelled or moved, or a new shift is added,
/// every matching entry gets one email and closes. Booking is then first come, first served — the existing
/// storage constraints decide who gets the slot.
/// </summary>
public sealed class WaitlistService(IUnitOfWorkFactory units, UserAccess access, ClinicClock clock,
    IOptions<NotificationOptions> options) : IWaitlistNotifier
{
    public const int MaxActiveEntries = 10;

    public async Task<Result<WaitlistEntryDto>> JoinAsync(JoinWaitlistDto dto)
    {
        var patientId = await access.PatientIdAsync();
        var before = DateTime.SpecifyKind(dto.Before, DateTimeKind.Unspecified);
        if (before <= clock.MinimumBookingTime)
            return Result.Failure<WaitlistEntryDto>("Wybierz datę późniejszą niż najbliższy możliwy termin rezerwacji (24 godziny).");
        if (before > clock.Now.AddMonths(3))
            return Result.Failure<WaitlistEntryDto>("Na liście oczekujących można czekać najwyżej 3 miesiące.");
        using var unit = units.Create();
        var doctor = await unit.Doctors.GetByIdAsync(dto.DoctorId);
        if (doctor is null) return Result.Failure<WaitlistEntryDto>("Lekarz nie istnieje.", ErrorType.NotFound);
        var active = (await unit.Waitlist.FindAsync(e => e.PatientId == patientId && e.Active)).ToList();
        if (active.Any(e => e.DoctorId == dto.DoctorId))
            return Result.Failure<WaitlistEntryDto>("Czekasz już na termin u tego lekarza. Usuń wpis, aby zmienić datę.", ErrorType.Conflict);
        if (active.Count >= MaxActiveEntries)
            return Result.Failure<WaitlistEntryDto>($"Możesz czekać jednocześnie u najwyżej {MaxActiveEntries} lekarzy.", ErrorType.Conflict);
        var entry = new WaitlistEntry { PatientId = patientId, DoctorId = dto.DoctorId, Before = before, CreatedAtUtc = clock.UtcNow };
        await unit.Waitlist.AddAsync(entry);
        try { await unit.SaveChangesAsync(); }
        catch (PersistenceConflictException)
        { return Result.Failure<WaitlistEntryDto>("Czekasz już na termin u tego lekarza.", ErrorType.Conflict); }
        return Result.Success(new WaitlistEntryDto(entry.Id, doctor.Id, $"{doctor.FirstName} {doctor.LastName}", entry.Before, entry.CreatedAtUtc));
    }

    public async Task<Result<IReadOnlyList<WaitlistEntryDto>>> MineAsync()
    {
        var patientId = await access.PatientIdAsync();
        using var unit = units.Create();
        var entries = (await unit.Waitlist.FindAsync(e => e.PatientId == patientId && e.Active)).OrderBy(e => e.Before).ToList();
        var result = new List<WaitlistEntryDto>();
        foreach (var entry in entries)
        {
            var doctor = await unit.Doctors.GetByIdAsync(entry.DoctorId);
            result.Add(new WaitlistEntryDto(entry.Id, entry.DoctorId, doctor is null ? "" : $"{doctor.FirstName} {doctor.LastName}", entry.Before, entry.CreatedAtUtc));
        }
        return Result.Success<IReadOnlyList<WaitlistEntryDto>>(result);
    }

    public async Task<Result> LeaveAsync(int id)
    {
        var patientId = await access.PatientIdAsync();
        using var unit = units.Create();
        var entry = await unit.Waitlist.GetByIdAsync(id);
        if (entry is null || entry.PatientId != patientId || !entry.Active) return Result.Failure("Nie znaleziono wpisu.", ErrorType.NotFound);
        entry.Active = false;
        await unit.SaveChangesAsync();
        return Result.Success();
    }

    public async Task SlotFreedAsync(int doctorId, DateTime freedStart)
    {
        // Slots inside the 24-hour booking notice cannot be booked, so they are not worth an email.
        if (freedStart < clock.MinimumBookingTime) return;
        using var unit = units.Create();
        var matching = (await unit.Waitlist.FindAsync(e => e.DoctorId == doctorId && e.Active && e.Before > freedStart)).ToList();
        if (matching.Count == 0) return;
        var doctor = await unit.Doctors.GetByIdAsync(doctorId);
        var doctorName = doctor is null ? "lekarza" : $"{doctor.FirstName} {doctor.LastName}";
        foreach (var found in matching)
        {
            var entry = await unit.Waitlist.GetByIdAsync(found.Id);
            var patient = await unit.Patients.GetByIdAsync(found.PatientId);
            if (entry is null || patient is null) continue;
            entry.Active = false;
            entry.NotifiedAtUtc = clock.UtcNow;
            await unit.EmailOutbox.AddAsync(EmailTemplates.Create(patient.Email, "Zwolnił się wcześniejszy termin",
                "Jest wolny termin", $"U lekarza {doctorName} zwolnił się termin {freedStart:dd.MM.yyyy} o {freedStart:HH:mm}.",
                "Zarezerwuj wizytę", options.Value.Link($"/appointments/create?doctorId={doctorId}"),
                "Termin dostanie osoba, która pierwsza go zarezerwuje. To powiadomienie zamyka Twój wpis na liście oczekujących.", clock.UtcNow));
        }
        try { await unit.SaveChangesAsync(); }
        catch (PersistenceConflictException) { }
    }

    /// <summary>The first bookable start inside a new shift, if any.</summary>
    public DateTime? FirstBookableStart(DateTime shiftStart, DateTime shiftEnd)
    {
        var start = shiftStart < clock.MinimumBookingTime ? AppointmentSlots.RoundUp(clock.MinimumBookingTime) : shiftStart;
        return start.Add(AppointmentSlots.Duration) <= shiftEnd ? start : null;
    }
}
