using System.Globalization;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.Domain.Scheduling;
using MedicalBookingSystem.SharedKernel;
using Microsoft.Extensions.Options;

namespace MedicalBookingSystem.Application.Notifications;

public sealed record CalendarFile(string FileName, string ContentType, string Content);

public interface IVisitNotifications
{
    /// <summary>Queues the confirmation in the given unit of work; the caller saves it after the booking is stored.</summary>
    Task QueueBookingConfirmationAsync(IUnitOfWork unit, Appointment appointment);
}

/// <summary>Booking confirmations, day-before reminders and calendar files (.ics) for patients.</summary>
public sealed class VisitNotificationService(IUnitOfWorkFactory units, ClinicClock clock, UserAccess access,
    IOptions<NotificationOptions> options) : IVisitNotifications
{
    private static readonly CultureInfo Polish = CultureInfo.InvariantCulture;

    public async Task QueueBookingConfirmationAsync(IUnitOfWork unit, Appointment appointment)
    {
        var details = await DetailsAsync(unit, appointment);
        if (details is null) return;
        await unit.EmailOutbox.AddAsync(Message(details, "Potwierdzenie wizyty", "Wizyta zarezerwowana",
            $"Zarezerwowano wizytę u lekarza {details.Doctor} na {details.When}.", appointment.Id));
    }

    /// <summary>Queues reminders for tomorrow's scheduled visits that have not been reminded yet. Safe to run repeatedly.</summary>
    public async Task<int> QueueTomorrowRemindersAsync()
    {
        using var unit = units.Create();
        var from = clock.Today.AddDays(1);
        var to = from.AddDays(1);
        var due = await unit.Appointments.FindAsync(a => a.Status == AppointmentStatus.Scheduled && a.ReminderQueuedAtUtc == null &&
            a.Date >= from && a.Date < to);
        var queued = 0;
        foreach (var candidate in due)
        {
            var appointment = await unit.Appointments.GetByIdAsync(candidate.Id);
            if (appointment is null) continue;
            var details = await DetailsAsync(unit, appointment);
            if (details is null) continue;
            await unit.EmailOutbox.AddAsync(Message(details, "Przypomnienie: wizyta jutro", "Przypominamy o jutrzejszej wizycie",
                $"Jutro o {appointment.Date.ToString("HH:mm", Polish)} masz wizytę u lekarza {details.Doctor}.", appointment.Id));
            appointment.ReminderQueuedAtUtc = clock.UtcNow;
            queued++;
        }
        if (queued > 0) await unit.SaveChangesAsync();
        return queued;
    }

    public async Task<Result<CalendarFile>> CalendarFileAsync(int appointmentId)
    {
        var patientId = await access.PatientIdAsync();
        using var unit = units.Create();
        var appointment = await unit.Appointments.GetByIdAsync(appointmentId);
        if (appointment is null || appointment.PatientId != patientId)
            return Result.Failure<CalendarFile>("Nie znaleziono wizyty.", ErrorType.NotFound);
        if (appointment.Status != AppointmentStatus.Scheduled)
            return Result.Failure<CalendarFile>("Do kalendarza można dodać tylko zaplanowaną wizytę.", ErrorType.Conflict);
        var details = await DetailsAsync(unit, appointment);
        return details is null
            ? Result.Failure<CalendarFile>("Nie znaleziono wizyty.", ErrorType.NotFound)
            : Result.Success(new CalendarFile($"wizyta-{appointment.Id}.ics", CalendarInvite.ContentType, Invite(details, appointment.Id)));
    }

    private sealed record VisitDetails(Patient Patient, string Doctor, string When, string? Location, DateTime StartUtc, DateTime EndUtc);

    private async Task<VisitDetails?> DetailsAsync(IUnitOfWork unit, Appointment appointment)
    {
        var patient = await unit.Patients.GetByIdAsync(appointment.PatientId);
        var doctor = await unit.Doctors.GetByIdAsync(appointment.DoctorId);
        if (patient is null || doctor is null) return null;
        var clinic = appointment.ClinicId is int clinicId ? await unit.Clinics.GetByIdAsync(clinicId) : null;
        var location = appointment.IsRemote ? $"Teleporada: {VideoRooms.Link(appointment.VideoRoom)}"
            : clinic is null ? null : $"{clinic.Name}, {clinic.Address}";
        var startUtc = clock.ToUtc(appointment.Date);
        return new VisitDetails(patient, $"{doctor.FirstName} {doctor.LastName}",
            appointment.Date.ToString("dd.MM.yyyy 'o' HH:mm", Polish), location, startUtc, startUtc.Add(AppointmentSlots.Duration));
    }

    private EmailMessage Message(VisitDetails details, string subject, string heading, string intro, int? appointmentId)
    {
        var where = details.Location is null ? "" : $" Miejsce: {details.Location}.";
        var message = EmailTemplates.Create(details.Patient.Email, subject, heading, intro + where,
            "Moje wizyty", options.Value.Link("/appointments"),
            "W załączniku jest wydarzenie do kalendarza. Wizytę można zmienić lub odwołać najpóźniej 24 godziny wcześniej.", clock.UtcNow);
        if (appointmentId is int id)
        {
            message.AttachmentName = "wizyta.ics";
            message.AttachmentContentType = CalendarInvite.ContentType;
            message.AttachmentContent = Invite(details, id);
        }
        return message;
    }

    private string Invite(VisitDetails details, int appointmentId) => CalendarInvite.Create(appointmentId, details.StartUtc, details.EndUtc,
        $"Wizyta: {details.Doctor}", details.Location,
        $"Wizyta lekarska ({AppointmentSlots.Duration.TotalMinutes:0} min). Zarządzaj wizytą: {options.Value.Link("/appointments")}", clock.UtcNow);
}
