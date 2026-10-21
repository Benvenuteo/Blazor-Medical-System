using MedicalBookingSystem.Application.Mappings;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto;
using Microsoft.AspNetCore.Identity;
using PasswordVerificationResult = Microsoft.AspNetCore.Identity.PasswordVerificationResult;

namespace MedicalBookingSystem.Application.Services
{
    public class PatientService : IPatientService
    {
        private readonly IUnitOfWork _uow;

        private readonly IPasswordHasher<Patient> _passwordHasher;
        public PatientService(IUnitOfWork unitOfWork, IPasswordHasher<Patient> passwordHasher)
        {
            _uow = unitOfWork;

            _passwordHasher = passwordHasher;
        }

        public async Task<Result<PatientDto>> GetPatientProfileAsync(int id)
        {
            try
            {
                var patient = await _uow.Patients.GetByIdAsync(id);
                if (patient == null)
                    return Result<PatientDto>.Failure<PatientDto>("Pacjent nie został znaleziony");

                var patientDto = patient.ToDto();
                return Result<PatientDto>.Success(patientDto);
            }
            catch (Exception)
            {
                return Result<PatientDto>.Failure<PatientDto>("Wystąpił błąd podczas pobierania danych pacjenta");
            }
        }

        public async Task<Result<PatientDto>> LoginAsync(LoginDto dto)
        {
            try
            {
                var patient = await _uow.Patients.GetByEmailAsync(dto.Email);
                if (patient == null)
                    return Result<PatientDto>.Failure<PatientDto>("Nieprawidłowy email lub hasło");

                if (_passwordHasher.VerifyHashedPassword(patient, patient.PasswordHash, dto.Password) == PasswordVerificationResult.Failed)
                    return Result<PatientDto>.Failure<PatientDto>("Nieprawidłowy email lub hasło");

                var patientDto = patient.ToDto();
                return Result<PatientDto>.Success(patientDto);
            }
            catch (Exception)
            {
                return Result<PatientDto>.Failure<PatientDto>("Wystąpił błąd podczas logowania");
            }
        }

        public async Task<Result<PatientDto>> RegisterAsync(RegisterPatientDto dto)
        {
            if (await _uow.Patients.GetByEmailAsync(dto.Email) != null)
                return Result<PatientDto>.Failure<PatientDto>("Email jest już zajęty");

            var patient = new Patient { FirstName = dto.FirstName.Trim(), LastName = dto.LastName.Trim(), Email = dto.Email.Trim().ToLowerInvariant(), PhoneNumber = dto.PhoneNumber, DateOfBirth = dto.DateOfBirth };
            patient.PasswordHash = _passwordHasher.HashPassword(patient, dto.Password);

            await _uow.Patients.AddAsync(patient);
            await _uow.SaveChangesAsync();

            return Result<PatientDto>.Success(patient.ToDto());
        }

        public async Task<Result> UpdatePatientAsync(int id, UpdatePatientDto dto)
        {
            try
            {
                var patient = await _uow.Patients.GetByIdAsync(id);
                if (patient == null)
                    return Result.Failure("Pacjent nie został znaleziony");

                // Verify credentials before changing any tracked state.
                if (!string.IsNullOrEmpty(dto.NewPassword))
                {
                    if (string.IsNullOrEmpty(dto.CurrentPassword) ||
                        _passwordHasher.VerifyHashedPassword(patient, patient.PasswordHash, dto.CurrentPassword) == PasswordVerificationResult.Failed)
                        return Result.Failure("Aktualne hasło jest nieprawidłowe.");

                    patient.PasswordHash = _passwordHasher.HashPassword(patient, dto.NewPassword);
                }

                patient.FirstName = dto.FirstName ?? patient.FirstName;
                patient.LastName = dto.LastName ?? patient.LastName;
                patient.PhoneNumber = dto.PhoneNumber ?? patient.PhoneNumber;

                _uow.Patients.Update(patient);
                await _uow.SaveChangesAsync();

                return Result.Success();
            }
            catch (Exception)
            {
                return Result.Failure("Wystąpił błąd podczas aktualizacji danych");
            }
        }

    }
}
