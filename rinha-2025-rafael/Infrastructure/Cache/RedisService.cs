using rinha_2025_rafael.CrossCutting;
using rinha_2025_rafael.Domain;
using rinha_2025_rafael.Domain.Enum;
using StackExchange.Redis;
using System.Globalization;
using System.Text.Json;

namespace rinha_2025_rafael.Infrastructure.Cache
{
    public class RedisService : IRedisService
    {
        private const string PaymentQueueKey = "payments_queue";
        private readonly JsonSerializerOptions _jsonOptions;
        private const string SummaryHashKey = "summary"; // Chave para hash de resumo
        private const string DefaultPaymentsSetKey = "payments:default";
        private const string FallbackPaymentsSetKey = "payments:fallback";
        private readonly ILogger<RedisService> _logger;
        private readonly IDatabase _db;

        public RedisService(IConnectionMultiplexer redis, 
            ILogger<RedisService> logger,
            JsonSerializerOptions jsonOptions)
        {
            _db = redis.GetDatabase();
            _logger = logger;
            _jsonOptions = jsonOptions;
        }

        public async Task EnqueuePaymentAsync(PaymentRequest request)
        {
            _logger.LogInformation($"[REDIS] - Enfileirando pagamento {request.CorrelationId}");

            var payload = JsonSerializer.Serialize(request, _jsonOptions);

            // Adiciona elemento no início (à esquerda) para o Worker pegar o do final (à direita).
            await _db.ListLeftPushAsync(PaymentQueueKey, payload);
        }

        /// <summary>
        /// Reenfileira um pagamento no início da fila para que ele seja processado novamente.
        /// Usa LPUSH para garantir que ele tenha prioridade na próxima iteração do worker.
        /// </summary>
        public async Task RequeuePaymentAsync(PaymentRequest request)
        {
            var payload = JsonSerializer.Serialize(request, JsonContext.Default.PaymentRequest);

            // LPUSH para colocar de volta no início (à esquerda).
            await _db.ListLeftPushAsync(PaymentQueueKey, payload);
        }

        /// <summary>
        /// Registra um pagamento processado em um Sorted Set para permitir consultas por data.
        /// </summary>
        public async Task RecordPaymentAsync(ProcessorType processorType, PaymentRequest request, DateTime timestamp)
        {
            var key = processorType == ProcessorType.DEFAULT ? DefaultPaymentsSetKey : FallbackPaymentsSetKey;

            double score = new DateTimeOffset(timestamp).ToUnixTimeSeconds();

            string value = $"{request.CorrelationId}:{request.Amount.ToString(CultureInfo.InvariantCulture)}";

            await _db.SortedSetAddAsync(key, value, score);
        }

        /// <summary>
        /// Busca o resumo, aplicando o filtro de data se fornecido.
        /// </summary>
        public async Task<PaymentSummaryResponse> GetSummaryAsync(DateTime? from, DateTime? to)
        {
            var fromScore = from.HasValue ? new DateTimeOffset(from.Value).ToUnixTimeSeconds() : double.NegativeInfinity;
            var toScore = to.HasValue ? new DateTimeOffset(to.Value).ToUnixTimeSeconds() : double.PositiveInfinity;

            var defaultTask = GetSummaryForProcessorAsync(DefaultPaymentsSetKey, fromScore, toScore);
            var fallbackTask = GetSummaryForProcessorAsync(FallbackPaymentsSetKey, fromScore, toScore);

            await Task.WhenAll(defaultTask, fallbackTask);

            return new PaymentSummaryResponse(defaultTask.Result, fallbackTask.Result);
        }

        private async Task<SummaryDetails> GetSummaryForProcessorAsync(string key, double fromScore, double toScore)
        {
            // ZRANGEBYSCORE busca todos os membros dentro do intervalo de scores (timestamp)
            var entries = await _db.SortedSetRangeByScoreAsync(key, fromScore, toScore);

            if(entries.Length == 0)
            {
                return new SummaryDetails(0, 0);
            }

            long totalRequests = entries.Length;
            double totalAmount = 0;

            foreach(var entry in entries)
            {
                // Extrai o valor do amount da string formatada
                var parts = entry.ToString().Split(':');
                if(parts.Length == 2 && double.TryParse(parts[1], CultureInfo.InvariantCulture, out var amount))
                {
                    totalAmount += amount;
                }
            }

            return new SummaryDetails(totalRequests, totalAmount);
        }

    }
}
