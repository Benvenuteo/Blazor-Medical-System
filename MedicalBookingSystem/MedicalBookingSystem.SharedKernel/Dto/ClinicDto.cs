namespace MedicalBookingSystem.SharedKernel.Dto;

public sealed class ClinicDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public List<int> DoctorIds { get; set; } = [];
    public string Address => $"{Street}, {PostalCode} {City}".Trim(' ', ',');
}

public sealed class SaveClinicDto
{
    public string Name { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public List<int> DoctorIds { get; set; } = [];
}

/// <summary>Result of turning an address into coordinates (OpenStreetMap Nominatim).</summary>
public sealed record GeoPointDto(double Latitude, double Longitude, string DisplayName);

public static class MapDefaults
{
    public const double DefaultRadiusKm = 30;
    public const double MaxRadiusKm = 100;
}
