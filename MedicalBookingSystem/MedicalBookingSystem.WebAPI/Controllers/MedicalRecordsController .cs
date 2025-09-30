using FluentValidation;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.SharedKernel.Dto.NotesPrescriptionDto;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MedicalBookingSystem.WebAPI.Controllers
{
    [Route("api/medical-records")]
    [ApiController]
    public class MedicalRecordsController : ControllerBase
    {
        private readonly IMedicalRecordService _medicalRecordService;
        private readonly IValidator<CreatePrescriptionDto> _prescriptionValidator;
        private readonly IValidator<CreateNoteDto> _noteValidator;

        public MedicalRecordsController(
            IMedicalRecordService medicalRecordService,
            IValidator<CreatePrescriptionDto> prescriptionValidator,
            IValidator<CreateNoteDto> noteValidator)
        {
            _medicalRecordService = medicalRecordService;
            _prescriptionValidator = prescriptionValidator;
            _noteValidator = noteValidator;
        }

        [HttpGet("patient/{patientId}")]
        public async Task<IActionResult> GetPatientDocuments(int patientId)
        {
            var result = await _medicalRecordService.GetPatientDocumentsAsync(patientId);
            return Ok(result);
        }

        [HttpPost("prescriptions")]
        public async Task<IActionResult> CreatePrescription([FromBody] CreatePrescriptionDto dto)
        {
            var validationResult = await _prescriptionValidator.ValidateAsync(dto);
            if (!validationResult.IsValid)
                return BadRequest(validationResult.Errors.Select(e => e.ErrorMessage));

            var doctorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var result = await _medicalRecordService.CreatePrescriptionAsync(dto, doctorId);

            if (result.IsSuccess)
                return CreatedAtAction(nameof(GetPrescription), new { id = result.Value.Id }, result.Value);

            return BadRequest(result.Error);
        }

        [HttpPost("notes")]
        public async Task<IActionResult> AddNote([FromBody] CreateNoteDto dto, int doctorId)
        {
            var validationResult = await _noteValidator.ValidateAsync(dto);
            if (!validationResult.IsValid)
                return BadRequest(validationResult.Errors.Select(e => e.ErrorMessage));

            var result = await _medicalRecordService.AddNoteToAppointmentAsync(dto, doctorId);
            if (result.IsSuccess)
                return CreatedAtAction(nameof(GetNote), new { id = result.Value.Id }, result.Value);

            return BadRequest(result.Error);
        }

        [HttpGet("prescriptions/{id}")]
        public async Task<IActionResult> GetPrescription(int id)
        {
            var result = await _medicalRecordService.GetPrescriptionAsync(id);
            if (result.IsSuccess)
                return Ok(result.Value);

            return NotFound(result.Error);
        }

        [HttpGet("notes/{id}")]
        public async Task<IActionResult> GetNote(int id)
        {
            var result = await _medicalRecordService.GetNoteAsync(id);
            if (result.IsSuccess)
                return Ok(result.Value);

            return NotFound(result.Error);
        }
    }
}
