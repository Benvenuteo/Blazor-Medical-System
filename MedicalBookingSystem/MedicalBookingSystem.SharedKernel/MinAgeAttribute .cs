using System.ComponentModel.DataAnnotations;

namespace MedicalBookingSystem.SharedKernel
{
    public class MinAgeAttribute : ValidationAttribute
    {
        private readonly int _minAge;

        public MinAgeAttribute(int minAge)
        {
            _minAge = minAge;
            ErrorMessage = $"Musisz mieć co najmniej {_minAge} lat.";
        }

        public override bool IsValid(object? value)
        {
            if (value is not DateTime date) return false;
            var today = DateTime.Today;
            var age = today.Year - date.Year;
            if (date > today.AddYears(-age)) age--;

            return age >= _minAge;
        }
    }
}