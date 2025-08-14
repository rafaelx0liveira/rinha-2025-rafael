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
        private readonly IDatabase _db;
        private const string PaymentQueueKey = "payments_queue";
        private const string SummaryHashKey = "summary"; // Chave para hash de resumo

        public RedisService(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        public async Task EnqueuePaymentAsync(PaymentRequest request)
        {
            var payload = JsonSerializer.Serialize(request, JsonContext.Default.PaymentRequest);

            // Adiciona elemento no início (à esquerda) para o Worker pegar o do final (à direita).
            await _db.ListLeftPushAsync(PaymentQueueKey, payload);
        }

        /// <summary>
        /// Atualiza atomicamente o resumo de pagamentos no Redis.
        /// </summary>
        public async Task UpdateSummaryAsync(ProcessorType processorType, decimal amount)
        {
            var processorName = processorType.ToString().ToLower();

            // Nomes dos campos dentro do Hash
            var totalRequestsField = $"{processorName}:totalRequests";
            var totalAmountField = $"{processorName}:totalAmount";

            // Cria uma transação para garantir que ambas as atualizações sejam atômicas.
            // Embora os comandos INCR sejam atômicos, usar uma transação (BATCH/EXEC) garante
            // que nenhuma outra operação possa ocorrer entre eles.
            var tran = _db.CreateTransaction();

            // Incrementa o contador de requisições.
            _ = tran.HashIncrementAsync(SummaryHashKey, totalRequestsField, 1);

            // Incrementa o valor total. Note a conversão para double.
            _ = tran.HashIncrementAsync(SummaryHashKey, totalAmountField, (double)amount);

            // Executa a transação.
            await tran.ExecuteAsync();
        }

        /// <summary>
        /// Busca os dados do resumo do Redis e os mapeia para o objeto de resposta.
        /// </summary>
        public async Task<PaymentSummaryResponse> GetSummaryAsync()
        {
            var summaryHash = await _db.HashGetAllAsync(SummaryHashKey);

            if (summaryHash.Length == 0)
            {
                // Se não houver dados, retorna um objeto zerado.
                return new PaymentSummaryResponse(new SummaryDetails(0, 0), new SummaryDetails(0, 0));
            }

            // Converte o array de HashEntry para um dicionário para facilitar o acesso.
            var summaryDict = summaryHash.ToDictionary(h => h.Name.ToString(), h => h.Value);

            // Lê os valores do dicionário, fazendo o parse para os tipos corretos.
            var defaultDetails = new SummaryDetails(
                long.Parse(summaryDict.GetValueOrDefault("default:totalRequests", "0")!),
                double.Parse(summaryDict.GetValueOrDefault("default:totalAmount", "0.0")!, CultureInfo.InvariantCulture)
            );

            var fallbackDetails = new SummaryDetails(
                long.Parse(summaryDict.GetValueOrDefault("fallback:totalRequests", "0")!),
                double.Parse(summaryDict.GetValueOrDefault("fallback:totalAmount", "0.0")!, CultureInfo.InvariantCulture)
            );

            return new PaymentSummaryResponse(defaultDetails, fallbackDetails);
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
    }
}
