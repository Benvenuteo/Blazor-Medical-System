using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MedicalBookingSystem.Infrastructure;
using MedicalBookingSystem.IntegrationTests.Support;
using MedicalBookingSystem.SharedKernel.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalBookingSystem.IntegrationTests.Api;

public sealed class ApiContractTests
{
    [Theory]
    [InlineData("/api/doctors?Page=0", 400)]
    [InlineData("/api/doctors?PageSize=51", 400)]
    [InlineData("/api/doctors?Page=not-a-number", 400)]
    [InlineData("/api/doctors?SortBy=invalid", 400)]
    [InlineData("/api/doctors?Date=9999-12-31", 400)]
    [InlineData("/api/doctors/1/slots?date=9999-12-31", 400)]
    [InlineData("/api/doctors/999999", 404)]
    [InlineData("/api/patients/profile", 401)]
    public async Task BindingValidationMissingResourceAndChallenge_ReturnProblemDetails(string path, int status)
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = scenario.Factory.CreateClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(status, body.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("title").GetString()));
        Assert.True(body.RootElement.TryGetProperty("traceId", out _));
        Assert.True(body.RootElement.TryGetProperty("instance", out _));
        Assert.False(body.RootElement.TryGetProperty("isSuccess", out _));
    }

    [Fact]
    public async Task DetailEndpoints_ReturnDtoWithoutApplicationResultEnvelope()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = scenario.Factory.CreateClient();
        foreach (var path in new[] { $"/api/doctors/{scenario.DoctorId}", $"/api/reviews/{scenario.ReviewId}" })
        {
            using var response = await client.GetAsync(path);
            response.EnsureSuccessStatusCode();
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(body.RootElement.TryGetProperty("id", out _));
            Assert.False(body.RootElement.TryGetProperty("value", out _));
            Assert.False(body.RootElement.TryGetProperty("isSuccess", out _));
        }
    }

    [Fact]
    public async Task UnexpectedStorageFailure_Returns500WithoutSqlOrStackTrace()
    {
        using var scenario = await SecurityScenario.CreateAsync();
        using var client = await scenario.SignInAsync();
        using (var scope = scenario.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.ExecuteSqlRawAsync("ALTER TABLE Patients RENAME TO UnavailablePatients");
        using var response = await client.GetAsync("/api/patients/profile");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SQLite", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Patients", body);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
    }
}
