using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MedicalBookingSystem.WebAPI.JWT;

public sealed class JwtService(IOptions<JwtSettings> settings, ClinicClock clock)
{
    public string GenerateToken(MedicalBookingSystem.SharedKernel.Dto.PatientDto patient) => GenerateToken(AccountClaims.Create(patient.Id,
        $"{patient.FirstName} {patient.LastName}", patient.Email, Roles.Patient, new DateTimeOffset(clock.UtcNow).AddHours(1)));

    public string GenerateToken(ClaimsPrincipal principal)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Value.SecretKey));
        var expires = DateTimeOffset.FromUnixTimeSeconds(long.Parse(
            principal.FindFirst(AccountClaims.ExpiresAt)?.Value!, CultureInfo.InvariantCulture));
        var token = new JwtSecurityToken(settings.Value.Issuer, settings.Value.Audience,
            principal.Claims, notBefore: clock.UtcNow, expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
