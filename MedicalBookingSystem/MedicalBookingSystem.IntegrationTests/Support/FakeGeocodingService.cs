using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.SharedKernel.Dto;

namespace MedicalBookingSystem.IntegrationTests.Support;

/// <summary>Offline stand-in for Nominatim: tests never call the public geocoder.</summary>
internal sealed class FakeGeocodingService : IGeocodingService
{
    public const double KrakowLatitude = 50.0614;
    public const double KrakowLongitude = 19.9366;

    public Task<GeoPointDto?> GeocodeAsync(string query, CancellationToken cancellationToken = default) =>
        Task.FromResult<GeoPointDto?>(query.Contains("kraków", StringComparison.OrdinalIgnoreCase)
            ? new GeoPointDto(KrakowLatitude, KrakowLongitude, "Kraków, województwo małopolskie, Polska")
            : null);
}
