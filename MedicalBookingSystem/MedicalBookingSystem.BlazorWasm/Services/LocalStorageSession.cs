using Blazored.LocalStorage;

namespace MedicalBookingSystem.BlazorWasm.Services;

public static class LocalStorageSession
{
    public static async Task<int> GetRequiredPatientIdAsync(this ILocalStorageService storage)
    {
        var value = await storage.GetItemAsync<string>("patientId");
        if (!int.TryParse(value, out var patientId) || patientId <= 0)
            throw new InvalidOperationException("Sesja wygasła. Zaloguj się ponownie.");
        return patientId;
    }
}
