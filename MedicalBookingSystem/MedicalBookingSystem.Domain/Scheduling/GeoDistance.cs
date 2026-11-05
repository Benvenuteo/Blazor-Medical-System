namespace MedicalBookingSystem.Domain.Scheduling;

/// <summary>Great-circle distance on a spherical Earth; accurate to well under 1% at city scale.</summary>
public static class GeoDistance
{
    public const double EarthRadiusKm = 6371.0088;
    // Must match EarthRadiusKm, otherwise the box is slightly smaller than the circle it should contain.
    private const double KmPerDegreeLatitude = EarthRadiusKm * Math.PI / 180;
    private const double Margin = 1.01;

    public static double Kilometres(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        static double Radians(double degrees) => degrees * Math.PI / 180;
        var dLat = Radians(latitude2 - latitude1);
        var dLon = Radians(longitude2 - longitude1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(Radians(latitude1)) * Math.Cos(Radians(latitude2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    /// <summary>
    /// Latitude/longitude rectangle that contains the whole circle. It is a cheap pre-filter for the database;
    /// the exact distance is checked afterwards. Longitude degrees shrink towards the poles, hence the cosine.
    /// </summary>
    public static (double MinLatitude, double MaxLatitude, double MinLongitude, double MaxLongitude) BoundingBox(
        double latitude, double longitude, double radiusKm)
    {
        var dLat = radiusKm * Margin / KmPerDegreeLatitude;
        var cos = Math.Max(Math.Cos(latitude * Math.PI / 180), 0.01);
        var dLon = Math.Min(radiusKm * Margin / (KmPerDegreeLatitude * cos), 180);
        return (Math.Max(latitude - dLat, -90), Math.Min(latitude + dLat, 90), longitude - dLon, longitude + dLon);
    }
}
