using rinha_2025_rafael.CrossCutting;
using rinha_2025_rafael.Domain;
using rinha_2025_rafael.Domain.Enum;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace rinha_2025_rafael.Infrastructure.Clients
{
    public class PaymentProcessorClient : IPaymentProcessorClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PaymentProcessorClient> _logger;
        private readonly IConfiguration _configuration;

        private readonly string _defaultUri;
        private readonly string _fallbackUri;

        public PaymentProcessorClient(HttpClient httpClient, ILogger<PaymentProcessorClient> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;

            _defaultUri = _configuration["PaymentProcessorUri:Default"] ?? throw new ArgumentNullException("Default URI is missing");

            _fallbackUri = _configuration["PaymentProcessorUri:Fallback"] ?? throw new ArgumentNullException("Fallback URI is missing");
        }

        /// <summary>
        /// Envia um pagamento para o processador especificado.
        /// </summary>
        public async Task ProcessPaymentAsync(PaymentRequest request, ProcessorType processorType)
        {
            var processorPayload = new
            {
                request.CorrelationId,
                request.Amount,
                RequestedAt = DateTime.UtcNow,
            };

            var uri = GetProcessorUri(processorType, "/payments");
            _logger.LogInformation($"[CLIENT] - Enviando pagamento {request.CorrelationId} para {uri}");

            var jsonPayload = JsonSerializer.Serialize(processorPayload, JsonContext.DefaultOptions);

            using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(uri, content);

            // Lança uma exceção se a resposta não for de sucesso (2xx).
            // Será capturado pelo try/catch do Worker, e então acionará o Circuit Breaker.
            response.EnsureSuccessStatusCode();
        }

        public async Task<HealthCheckResponse?> GetHealthAsync(ProcessorType processorType)
        {
            var uri = GetProcessorUri(processorType, "/payments/service-health");

            try
            {
                var responseStream = await _httpClient.GetStreamAsync(uri);

                var healthResponse = await JsonSerializer.DeserializeAsync<HealthCheckResponse>(responseStream, JsonContext.DefaultOptions);
                return healthResponse;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"[CLIENT] - Falha ao obter health check de {uri}");
                throw;
            }
        }

        public Uri GetProcessorUri(ProcessorType processorType, string path)
        {
            var baseAddress = processorType == ProcessorType.DEFAULT
                ? _defaultUri
                : _fallbackUri;

            return new Uri($"{baseAddress}{path}");
        }
    }
}
