namespace MedicalBookingSystem.Domain.Models;

/// <summary>A place where doctors see patients. Coordinates are set by an administrator (pin on the map).</summary>
public sealed class Clinic
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public ICollection<DoctorClinic> DoctorClinics { get; set; } = new List<DoctorClinic>();

    public string Address => $"{Street}, {PostalCode} {City}".Trim(' ', ',');
}

public sealed class DoctorClinic
{
    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;
    public int ClinicId { get; set; }
    public Clinic Clinic { get; set; } = null!;
}
