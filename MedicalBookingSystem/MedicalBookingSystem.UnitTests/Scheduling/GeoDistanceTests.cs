using MedicalBookingSystem.Domain.Scheduling;

namespace MedicalBookingSystem.UnitTests.Scheduling;

public sealed class GeoDistanceTests
{
    [Fact]
    public void Kilometres_WarsawToKrakow_MatchesTheKnownDistance()
    {
        var distance = GeoDistance.Kilometres(52.2297, 21.0122, 50.0647, 19.9450);

        Assert.InRange(distance, 250, 255);
    }

    [Fact]
    public void Kilometres_IsZeroForTheSamePoint_AndSymmetric()
    {
        Assert.Equal(0, GeoDistance.Kilometres(50, 20, 50, 20), 9);
        Assert.Equal(GeoDistance.Kilometres(50, 20, 51, 21), GeoDistance.Kilometres(51, 21, 50, 20), 9);
    }

    [Theory]
    [InlineData(50.06, 19.94, 30)]
    [InlineData(69.65, 18.96, 100)] // far north: longitude degrees are much shorter
    [InlineData(-33.87, 151.21, 50)]
    public void BoundingBox_ContainsEveryPointOfTheCircle(double latitude, double longitude, double radius)
    {
        var box = GeoDistance.BoundingBox(latitude, longitude, radius);
        for (var bearing = 0; bearing < 360; bearing += 10)
        {
            // Destination point at exactly `radius` km on the given bearing (spherical formula).
            var d = radius / GeoDistance.EarthRadiusKm;
            var b = bearing * Math.PI / 180;
            var lat1 = latitude * Math.PI / 180;
            var lon1 = longitude * Math.PI / 180;
            var lat2 = Math.Asin(Math.Sin(lat1) * Math.Cos(d) + Math.Cos(lat1) * Math.Sin(d) * Math.Cos(b));
            var lon2 = lon1 + Math.Atan2(Math.Sin(b) * Math.Sin(d) * Math.Cos(lat1), Math.Cos(d) - Math.Sin(lat1) * Math.Sin(lat2));
            Assert.InRange(lat2 * 180 / Math.PI, box.MinLatitude, box.MaxLatitude);
            Assert.InRange(lon2 * 180 / Math.PI, box.MinLongitude, box.MaxLongitude);
        }
    }
}
