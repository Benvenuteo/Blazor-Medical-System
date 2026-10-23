using System.Net;
using MedicalBookingSystem.IntegrationTests.Support;

namespace MedicalBookingSystem.IntegrationTests.Api;

public sealed class ApiSmokeTests
{
    [Fact]
    public async Task HealthEndpoint_ReturnsSuccessWithoutDatabaseInitialization()
    {
        using var factory = new MedicalApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnknownDoctor_ReturnsNotFound()
    {
        using var factory = new MedicalApiFactory();
        await factory.InitializeDatabaseAsync();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/doctors/404");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
