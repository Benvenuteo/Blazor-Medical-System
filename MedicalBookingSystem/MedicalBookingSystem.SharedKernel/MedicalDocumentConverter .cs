using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Dto.NotesPrescriptionDto;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MedicalBookingSystem.SharedKernel
{
    public class MedicalDocumentConverter : JsonConverter<MedicalDocumentDto>
    {
        public override MedicalDocumentDto Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var jsonDoc = JsonDocument.ParseValue(ref reader);
            var root = jsonDoc.RootElement;

            if (!root.TryGetProperty("documentType", out var typeProperty))
            {
                throw new JsonException("Missing discriminator property 'documentType'.");
            }

            var type = typeProperty.GetString();
            MedicalDocumentDto result = type switch
            {
                "note" => JsonSerializer.Deserialize<NoteDto>(root.GetRawText(), options),
                "prescription" => JsonSerializer.Deserialize<PrescriptionDto>(root.GetRawText(), options),
                _ => throw new JsonException($"Unknown document type: {type}")
            };

            return result!;
        }

        public override void Write(Utf8JsonWriter writer, MedicalDocumentDto value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, (object)value!, value.GetType(), options);
        }
    }
}
