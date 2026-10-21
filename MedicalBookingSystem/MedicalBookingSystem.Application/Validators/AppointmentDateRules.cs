using FluentValidation;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Scheduling;

namespace MedicalBookingSystem.Application.Validators;

public static class AppointmentDateRules
{
    public static IRuleBuilderOptions<T, DateTime> BookableClinicTime<T>(this IRuleBuilder<T, DateTime> rule, ClinicClock clock) =>
        rule.NotEmpty().WithMessage("Data wizyty jest wymagana.")
            .Must(date => date.Kind == DateTimeKind.Unspecified).WithMessage("Podaj lokalny czas placówki bez sufiksu UTC ani offsetu.")
            .Must(clock.IsUnambiguousLocalTime).WithMessage("Termin jest niejednoznaczny lub nie istnieje z powodu zmiany czasu.")
            .Must(AppointmentSlots.IsAligned).WithMessage("Wizyty rozpoczynają się o pełnej lub połowie godziny.")
            .GreaterThanOrEqualTo(_ => clock.MinimumBookingTime).WithMessage("Wizytę należy rezerwować co najmniej 24 godziny wcześniej.")
            .LessThanOrEqualTo(_ => clock.Now.AddMonths(3)).WithMessage("Termin nie może być odległy o więcej niż 3 miesiące.")
            .Must(date => clock.IsUnambiguousLocalTime(date.Add(AppointmentSlots.Duration)))
            .WithMessage("Cała wizyta musi mieścić się poza zmianą czasu.");
}
