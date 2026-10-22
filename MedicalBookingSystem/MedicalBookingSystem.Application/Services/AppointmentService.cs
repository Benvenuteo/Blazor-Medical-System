using FluentValidation;
using MedicalBookingSystem.Application.Mappings;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Exceptions;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.Domain.Scheduling;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;

namespace MedicalBookingSystem.Application.Services;

public sealed class AppointmentService(
    IUnitOfWorkFactory units, ClinicClock clock, UserAccess access,
    IValidator<CreateAppointmentDto> createValidator, IValidator<UpdateAppointmentDto> updateValidator) : IAppointmentService
{
    public async Task<Result<AppointmentDto>> CreateAppointmentAsync(CreateAppointmentDto dto, int patientId)
    {
        using var unitOfWork = units.Create();
        await access.RequirePatientAsync(patientId);
        var validation = await createValidator.ValidateAsync(dto);
        if (!validation.IsValid) return Result.Failure<AppointmentDto>(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        if (await unitOfWork.Doctors.GetByIdAsync(dto.DoctorId) is null)
            return Result.Failure<AppointmentDto>("Lekarz nie istnieje.", ErrorType.NotFound);
        if (!await unitOfWork.DoctorSchedules.IsTimeSlotAvailableAsync(dto.DoctorId, dto.Date))
            return Result.Failure<AppointmentDto>("Termin jest zajęty albo nie mieści się w grafiku. Wybierz inny termin.", ErrorType.Conflict);
        if (await PatientHasConflictAsync(unitOfWork, patientId, dto.Date))
            return Result.Failure<AppointmentDto>("W tym czasie masz już inną wizytę.", ErrorType.Conflict);
        var appointment = new Appointment { PatientId = patientId, DoctorId = dto.DoctorId, Date = dto.Date };
        await unitOfWork.Appointments.AddAsync(appointment);
        try { await unitOfWork.SaveChangesAsync(); }
        catch (PersistenceConflictException)
        {
            return Result.Failure<AppointmentDto>("Termin został już zajęty. Odśwież dostępne terminy.", ErrorType.Conflict);
        }
        return Result.Success(appointment.ToDto());
    }

    public async Task<Result> CancelAppointmentAsync(int id, int patientId)
    {
        using var unitOfWork = units.Create();
        await access.RequirePatientAsync(patientId);
        var appointment = await unitOfWork.Appointments.GetByIdAsync(id);
        if (appointment is null) return Result.Failure("Nie znaleziono wizyty.", ErrorType.NotFound);
        await access.RequirePatientAsync(appointment.PatientId);
        var editable = CheckPatientChange(appointment);
        if (editable.IsFailure) return editable;
        appointment.Status = AppointmentStatus.Cancelled;
        return await SaveChangeAsync(unitOfWork);
    }

    public async Task<Result> UpdateAppointmentAsync(int id, UpdateAppointmentDto dto)
    {
        using var unitOfWork = units.Create();
        await access.PatientIdAsync();
        var appointment = await unitOfWork.Appointments.GetByIdAsync(id);
        if (appointment is null) return Result.Failure("Nie znaleziono wizyty.", ErrorType.NotFound);
        await access.RequirePatientAsync(appointment.PatientId);
        var editable = CheckPatientChange(appointment);
        if (editable.IsFailure) return editable;
        var validation = await updateValidator.ValidateAsync(dto);
        if (!validation.IsValid) return Result.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        if (appointment.Date == dto.NewDate) return Result.Success();
        if (!await unitOfWork.DoctorSchedules.IsTimeSlotAvailableAsync(appointment.DoctorId, dto.NewDate, appointment.Id) ||
            await PatientHasConflictAsync(unitOfWork, appointment.PatientId, dto.NewDate, appointment.Id))
            return Result.Failure("Wybrany termin jest zajęty albo nie mieści się w grafiku.", ErrorType.Conflict);
        appointment.Date = dto.NewDate;
        return await SaveChangeAsync(unitOfWork);
    }

    public async Task<Result> CompleteAppointmentAsync(int id)
    {
        using var unitOfWork = units.Create();
        await access.DoctorIdAsync();
        var appointment = await unitOfWork.Appointments.GetByIdAsync(id);
        if (appointment is null) return Result.Failure("Nie znaleziono wizyty.", ErrorType.NotFound);
        await access.RequireDoctorAsync(appointment.DoctorId);
        if (appointment.Status != AppointmentStatus.Scheduled)
            return Result.Failure("Można zakończyć wyłącznie zaplanowaną wizytę.", ErrorType.Conflict);
        if (appointment.Date > clock.Now.Subtract(AppointmentSlots.Duration))
            return Result.Failure("Wizytę można oznaczyć jako odbytą po zakończeniu jej terminu.", ErrorType.Conflict);
        appointment.Status = AppointmentStatus.Completed;
        return await SaveChangeAsync(unitOfWork);
    }

    public async Task<Result<IEnumerable<AppointmentDto>>> GetPastAppointmentsAsync(int patientId)
    {
        using var unitOfWork = units.Create();
        await access.RequirePatientAsync(patientId);
        return Result.Success<IEnumerable<AppointmentDto>>((await unitOfWork.Appointments.GetHistoryByPatientIdAsync(patientId)).Select(a => a.ToDto()).ToList());
    }

    public async Task<Result<IEnumerable<AppointmentDto>>> GetUpcomingAppointmentsAsync(int patientId)
    {
        using var unitOfWork = units.Create();
        await access.RequirePatientAsync(patientId);
        return Result.Success<IEnumerable<AppointmentDto>>((await unitOfWork.Appointments.GetUpcomingByPatientIdAsync(patientId)).Select(a => a.ToDto()).ToList());
    }

    public async Task<Result<AppointmentDetailsDto>> GetAppointmentDetailsAsync(int appointmentId, int patientId)
    {
        using var unitOfWork = units.Create();
        await access.RequirePatientAsync(patientId);
        var appointment = await unitOfWork.Appointments.GetAppointmentByIdAsync(appointmentId);
        if (appointment is null) return Result.Failure<AppointmentDetailsDto>("Nie znaleziono wizyty.", ErrorType.NotFound);
        await access.RequirePatientAsync(appointment.PatientId);
        return Result.Success(appointment.ToDetailsDto());
    }

    public async Task<List<AppointmentDto>> GetScheduledAppointmentsByDoctorIdAsync(int doctorId)
    {
        using var unitOfWork = units.Create();
        await access.RequireDoctorAsync(doctorId);
        return (await unitOfWork.Appointments.GetScheduledByDoctorIdAsync(doctorId)).Select(a => a.ToDto()).ToList();
    }

    public async Task<List<AppointmentDto>> GetCompletedAppointmentsByDoctorIdAsync(int doctorId)
    {
        using var unitOfWork = units.Create();
        await access.RequireDoctorAsync(doctorId);
        return (await unitOfWork.Appointments.GetCompletedByDoctorIdAsync(doctorId)).Select(a => a.ToDto()).ToList();
    }

    private Result CheckPatientChange(Appointment appointment)
    {
        if (appointment.Status != AppointmentStatus.Scheduled)
            return Result.Failure("Można zmienić lub anulować wyłącznie zaplanowaną wizytę.", ErrorType.Conflict);
        return appointment.Date < clock.MinimumBookingTime
            ? Result.Failure("Zmiana lub anulowanie wymaga co najmniej 24 godzin do wizyty.", ErrorType.Conflict)
            : Result.Success();
    }

    private static Task<bool> PatientHasConflictAsync(IUnitOfWork unitOfWork, int patientId, DateTime date, int? excludedId = null)
    {
        var earliestOverlap = date.Subtract(AppointmentSlots.Duration);
        var end = date.Add(AppointmentSlots.Duration);
        return unitOfWork.Appointments.AnyAsync(a => a.PatientId == patientId && a.Id != excludedId &&
            a.Status != AppointmentStatus.Cancelled && a.Date > earliestOverlap && a.Date < end);
    }

    private static async Task<Result> SaveChangeAsync(IUnitOfWork unitOfWork)
    {
        try { await unitOfWork.SaveChangesAsync(); return Result.Success(); }
        catch (PersistenceConflictException)
        {
            return Result.Failure("Wizyta lub wybrany termin zmieniły się w innym żądaniu. Odśwież listę.", ErrorType.Conflict);
        }
    }
}
