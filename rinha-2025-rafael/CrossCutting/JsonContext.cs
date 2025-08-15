using rinha_2025_rafael.Domain;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace rinha_2025_rafael.CrossCutting
{
    // Este atributo informa ao compilador para gerar o código de serialização para este contexto.
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false)]
    // Adicionando um atributo [JsonSerializable] para CADA tipo que precisamos converter de/para JSON.
    [JsonSerializable(typeof(PaymentRequest))]
    [JsonSerializable(typeof(HealthCheckResponse))]
    [JsonSerializable(typeof(PaymentProcessorPayload))]
    [JsonSerializable(typeof(PaymentSummaryResponse))]
    [JsonSerializable(typeof(SummaryDetails))]
    public partial class JsonContext : JsonSerializerContext
    {
        public static JsonSerializerOptions DefaultOptions { get; } = new()
        {
            TypeInfoResolver = JsonContext.Default
        };
    }
}
