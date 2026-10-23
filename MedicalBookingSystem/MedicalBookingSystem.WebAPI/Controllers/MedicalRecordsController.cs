using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.SharedKernel.Security;
using Microsoft.AspNetCore.Authorization;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.SharedKernel.Dto.NotesPrescriptionDto;
using Microsoft.AspNetCore.Mvc;

namespace MedicalBookingSystem.WebAPI.Controllers
{
    [Route("api/medical-records")]
    [ApiController]
    [Authorize]
    public class MedicalRecordsController : ControllerBase
    {
        private readonly WorklistService _lists;
        private readonly UserAccess _access;
        private readonly IMedicalRecordService _medicalRecordService;

        public MedicalRecordsController(IMedicalRecordService medicalRecordService, UserAccess access, WorklistService lists)
        {
            _lists = lists;
            _access = access;
            _medicalRecordService = medicalRecordService;
        }

        [Authorize(Roles = Roles.Patient)]
        [HttpGet("patient/{patientId}")]
        public async Task<IActionResult> GetPatientDocuments(int patientId, [FromQuery] PageQueryDto query)
        {
            var result = await _lists.DocumentsAsync(patientId, query);
            return result.IsSuccess ? Ok(result.Value) : this.FailureResponse(result);
        }

        [Authorize(Roles = Roles.Doctor)]
        [HttpPost("prescriptions")]
        public async Task<IActionResult> CreatePrescription([FromBody] CreatePrescriptionDto dto)
        {
            var doctorId = await _access.DoctorIdAsync();
            var result = await _medicalRecordService.CreatePrescriptionAsync(dto, doctorId);

            if (result.IsSuccess)
                return CreatedAtAction(nameof(GetPrescription), new { id = result.Value.Id }, result.Value);

            return this.FailureResponse(result);
        }

        [Authorize(Roles = Roles.Doctor)]
        [HttpPost("notes")]
        public async Task<IActionResult> AddNote([FromBody] CreateNoteDto dto)
        {
            var doctorId = await _access.DoctorIdAsync();
            var result = await _medicalRecordService.AddNoteToAppointmentAsync(dto, doctorId);
            if (result.IsSuccess)
                return CreatedAtAction(nameof(GetNote), new { id = result.Value.Id }, result.Value);

            return this.FailureResponse(result);
        }

        [Authorize(Roles = Roles.Patient + "," + Roles.Doctor)]
        [HttpGet("prescriptions/{id}")]
        public async Task<IActionResult> GetPrescription(int id)
        {
            var result = await _medicalRecordService.GetPrescriptionAsync(id);
            if (result.IsSuccess)
                return Ok(result.Value);

            return this.FailureResponse(result);
        }

        [Authorize(Roles = Roles.Patient + "," + Roles.Doctor)]
        [HttpGet("notes/{id}")]
        public async Task<IActionResult> GetNote(int id)
        {
            var result = await _medicalRecordService.GetNoteAsync(id);
            if (result.IsSuccess)
                return Ok(result.Value);

            return this.FailureResponse(result);
        }
    }
}
