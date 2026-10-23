using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MedicalBookingSystem.IntegrationTests.Support;
using MedicalBookingSystem.SharedKernel.Dto;

namespace MedicalBookingSystem.IntegrationTests.Api;

public sealed class AuthenticationTests
{
    [Fact]
    public async Task RegisterAndLogin_UsesRealDatabaseAndReturnsTokenWithoutPasswordHash()
    {
        using var factory = new MedicalApiFactory();
        await factory.InitializeDatabaseAsync();
        using var client = factory.CreateClient();

        using var registration = await client.PostAsJsonAsync("/api/auth/register", ValidRegistration());
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        var body = await registration.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Original2026!", body);
        using var document = JsonDocument.Parse(body);
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("token").GetString()));

        using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = "PATIENT@EXAMPLE.TEST", Password = "Original2026!"
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateNormalizedEmail_ReturnsControlledError()
    {
        using var factory = new MedicalApiFactory();
        await factory.InitializeDatabaseAsync();
        using var client = factory.CreateClient();
        using var first = await client.PostAsJsonAsync("/api/auth/register", ValidRegistration());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var duplicate = ValidRegistration();
        duplicate.Email = "PATIENT@EXAMPLE.TEST";

        using var response = await client.PostAsJsonAsync("/api/auth/register", duplicate);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        using var factory = new MedicalApiFactory();
        await factory.InitializeDatabaseAsync();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = "missing@example.test", Password = "Incorrect2026!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidInput_ReturnsBadRequest()
    {
        using var factory = new MedicalApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterPatientDto
        {
            Email = "invalid", Password = "short", DateOfBirth = DateTime.Today, FirstName = "", LastName = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static RegisterPatientDto ValidRegistration() => new()
    {
        Email = "patient@example.test", Password = "Original2026!", FirstName = "Jan", LastName = "Testowy",
        PhoneNumber = "+48500000000", DateOfBirth = new DateTime(1990, 1, 1)
    };
}
