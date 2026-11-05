using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.SharedKernel.Dto;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedicalBookingSystem.Infrastructure.Geocoding;

public sealed class GeocodingOptions
{
    public string BaseUrl { get; set; } = "https://nominatim.openstreetmap.org/";
    /// <summary>Nominatim requires an identifying User-Agent; include a contact URL for the deployment.</summary>
    public string UserAgent { get; set; } = "MedicalBookingSystem/1.0 (+https://github.com/Benvenuteo/Blazor-Medical-System)";
    public string CountryCodes { get; set; } = "pl";
}

/// <summary>
/// Address search with OpenStreetMap Nominatim, following its usage policy: at most one request per second
/// from this process, an identifying User-Agent, results cached for a day and no autocomplete (search on submit).
/// Requests go through the API, so visitors' browsers never contact the geocoder directly.
/// </summary>
public sealed class NominatimGeocodingService(HttpClient http, IMemoryCache cache, IOptions<GeocodingOptions> options,
    ILogger<NominatimGeocodingService> logger) : IGeocodingService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTime lastRequestUtc = DateTime.MinValue;

    public async Task<GeoPointDto?> GeocodeAsync(string query, CancellationToken cancellationToken = default)
    {
        var key = "geocode:" + query.Trim().ToLowerInvariant();
        if (cache.TryGetValue(key, out GeoPointDto? cached)) return cached;
        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(key, out cached)) return cached;
            var wait = lastRequestUtc.AddSeconds(1) - DateTime.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, cancellationToken);
            lastRequestUtc = DateTime.UtcNow;
            var url = $"search?format=jsonv2&limit=1&addressdetails=0&countrycodes={Uri.EscapeDataString(options.Value.CountryCodes)}&q={Uri.EscapeDataString(query)}";
            var results = await http.GetFromJsonAsync<List<NominatimResult>>(url, cancellationToken) ?? [];
            var best = results.FirstOrDefault();
            GeoPointDto? point = best is null ? null : new GeoPointDto(
                double.Parse(best.Lat, CultureInfo.InvariantCulture), double.Parse(best.Lon, CultureInfo.InvariantCulture), best.DisplayName);
            cache.Set(key, point, TimeSpan.FromHours(24));
            return point;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or FormatException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            logger.LogWarning("Geocoding failed: {Reason}", exception.Message);
            return null;
        }
        finally { Gate.Release(); }
    }

    public static void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GeocodingOptions>(configuration.GetSection("Geocoding"));
        services.AddMemoryCache();
        services.AddHttpClient<IGeocodingService, NominatimGeocodingService>((provider, client) =>
        {
            var settings = provider.GetRequiredService<IOptions<GeocodingOptions>>().Value;
            client.BaseAddress = new Uri(settings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(8);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(settings.UserAgent);
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("pl");
        });
    }

    private sealed class NominatimResult
    {
        [JsonPropertyName("lat")] public string Lat { get; set; } = "0";
        [JsonPropertyName("lon")] public string Lon { get; set; } = "0";
        [JsonPropertyName("display_name")] public string DisplayName { get; set; } = string.Empty;
    }
}
