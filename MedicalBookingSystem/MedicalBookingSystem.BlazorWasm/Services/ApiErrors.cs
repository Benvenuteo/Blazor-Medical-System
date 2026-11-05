using System.Net;
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

    /// <summary>
    /// Turns a client-side exception into a message fit for patients. Technical details go to the
    /// browser console only, so status codes and stack details never appear in the page.
    /// </summary>
    public static string Describe(Exception exception, string failedAction)
    {
        Console.Error.WriteLine($"{failedAction}: {exception}");
        var reason = exception switch
        {
            HttpRequestException { StatusCode: HttpStatusCode.Unauthorized } => "Sesja wygasła. Zaloguj się ponownie.",
            HttpRequestException { StatusCode: HttpStatusCode.Forbidden } => "Brak dostępu do tych danych.",
            HttpRequestException { StatusCode: HttpStatusCode.NotFound } => "Nie znaleziono danych.",
            HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } => "Zbyt wiele prób. Spróbuj ponownie za chwilę.",
            HttpRequestException { StatusCode: >= HttpStatusCode.InternalServerError } => "Serwer jest chwilowo niedostępny. Spróbuj ponownie za chwilę.",
            HttpRequestException { StatusCode: null } => "Brak połączenia z serwerem. Sprawdź połączenie i spróbuj ponownie.",
            TaskCanceledException => "Serwer nie odpowiada. Spróbuj ponownie za chwilę.",
            JsonException or NotSupportedException => "Serwer zwrócił nieoczekiwaną odpowiedź.",
            _ => "Wystąpił nieoczekiwany błąd. Spróbuj ponownie."
        };
        return $"{failedAction}. {reason}";
    }
}
