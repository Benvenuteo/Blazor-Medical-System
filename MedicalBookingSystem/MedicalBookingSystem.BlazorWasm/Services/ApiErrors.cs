using System.Text.Json;

namespace MedicalBookingSystem.BlazorWasm.Services;

public static class ApiErrors
{
    public static async Task<string> ReadAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        try
        {
            using var json = JsonDocument.Parse(content);
            if (json.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (json.RootElement.TryGetProperty("detail", out var detail)) return detail.GetString() ?? "Operacja nie powiodła się.";
                if (json.RootElement.TryGetProperty("title", out var title)) return title.GetString() ?? "Operacja nie powiodła się.";
            }
        }
        catch (JsonException) { /* Older endpoints may still return plain-text errors. */ }
        return string.IsNullOrWhiteSpace(content) ? "Operacja nie powiodła się." : content;
    }
}
