using AutoMapper;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto;
using Microsoft.AspNet.Identity;
using PasswordVerificationResult = Microsoft.AspNet.Identity.PasswordVerificationResult;

namespace MedicalBookingSystem.Application.Services
{
    public class PatientService : IPatientService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;
        private readonly IPasswordHasher _passwordHasher;
        public PatientService(IUnitOfWork unitOfWork, IMapper mapper, IPasswordHasher passwordHasher)
        {
            _uow = unitOfWork;
            _mapper = mapper;
            _passwordHasher = passwordHasher;
        }

        public async Task<Result<PatientDto>> GetPatientProfileAsync(int id)
        {
            try
            {
                var patient = await _uow.Patients.GetByIdAsync(id);
                if (patient == null)
                    return Result<PatientDto>.Failure<PatientDto>("Pacjent nie został znaleziony");

                var patientDto = _mapper.Map<PatientDto>(patient);
                return Result<PatientDto>.Success(patientDto);
            }
            catch (Exception ex)
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

                if (_passwordHasher.VerifyHashedPassword(patient.PasswordHash, dto.Password) == PasswordVerificationResult.Failed)
                    return Result<PatientDto>.Failure<PatientDto>("Nieprawidłowy email lub hasło");

                var patientDto = _mapper.Map<PatientDto>(patient);
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

            var patient = _mapper.Map<Patient>(dto);
            patient.PasswordHash = _passwordHasher.HashPassword(dto.Password);

            await _uow.Patients.AddAsync(patient);
            await _uow.SaveChangesAsync();

            return Result<PatientDto>.Success(_mapper.Map<PatientDto>(patient));
        }

        public async Task<Result> UpdatePatientAsync(int id, UpdatePatientDto dto)
        {
            try
            {
                var patient = await _uow.Patients.GetByIdAsync(id);
                if (patient == null)
                    return Result.Failure("Pacjent nie został znaleziony");

                patient.FirstName = dto.FirstName ?? patient.FirstName;
                patient.LastName = dto.LastName ?? patient.LastName;
                patient.PhoneNumber = dto.PhoneNumber ?? patient.PhoneNumber;

                // Aktualizacja hasła jeśli podane
                if (!string.IsNullOrEmpty(dto.NewPassword))
                    patient.PasswordHash = _passwordHasher.HashPassword(dto.NewPassword);

                _uow.Patients.Update(patient);
                await _uow.SaveChangesAsync();

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure("Wystąpił błąd podczas aktualizacji danych");
            }
        }

    }
}
