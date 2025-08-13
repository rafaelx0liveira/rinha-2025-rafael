using rinha_2025_rafael.Domain.Enum;
using StackExchange.Redis;

namespace rinha_2025_rafael.Infrastructure.Resilience
{
    public class CircuitBreakerService : ICircuitBreakerService
    {
        private readonly IDatabase _database;

        // Tempo que o circuito ficará aberto
        private static readonly TimeSpan OpenToHalfOpenDuration = TimeSpan.FromSeconds(10);

        // Número de falhas consecutivas antes de abrir o circuito
        private const int FAILURE_THRESHOLD = 5;

        public CircuitBreakerService(IConnectionMultiplexer database)
        {
            _database = database.GetDatabase();
        }

        private string GetStateKey(ProcessorType processorType) => $"circuitbreaker: {processorType}: state";
        private string GetFailureCountKey(ProcessorType processorType) => $"circuitbreaker: {processorType}: failures";

        /// <summary>
        /// Verifica se o circuito está fechado (ou seja, se o serviço está disponível para uso).
        /// </summary>
        public async Task<bool> IsClosedAsync(ProcessorType processorType)
        {
            var state = await _database.StringGetAsync(GetStateKey(processorType));

            return !state.HasValue || state == "Closed";
        }

        /// <summary>
        /// Registra uma falha para um determinado processador.
        /// Se o número de falhas atingir o nosso limite (Threshold), o circuito é aberto.
        /// </summary>
        public async Task RecordFailureAsync(ProcessorType processorType)
        {
            var failureCount = await _database.StringIncrementAsync(GetFailureCountKey(processorType));

            // Atingiu o limite, abre o circuito
            if (failureCount >= FAILURE_THRESHOLD) 
            {
                await OpenCircuitAsync(processorType);
            }
        }

        /// <summary>
        /// Registra um sucesso, resetando o contador de falhas e fechando o circuito.
        /// </summary>
        public async Task RecordSuccessAsync(ProcessorType processorType)
        {
            // Remove a chave de contagem de falhas
            await _database.KeyDeleteAsync(GetFailureCountKey(processorType));

            // Garante que o estado volte para "Closed"
            await _database.StringSetAsync(GetStateKey(processorType), "Closed");
        }

        /// <summary>
        /// Abre o circuito e define um tempo de expiração para ele ir para o estado Meio-Aberto.
        /// </summary>
        public async Task OpenCircuitAsync(ProcessorType processorType)
        {
            await _database.StringSetAsync(GetStateKey(processorType), "Open", expiry: OpenToHalfOpenDuration);
        }


        /// <summary>
        /// Coloca o circuito em estado Meio-Aberto.
        /// Este método será chamado pelo nosso HealthCheckSentinel.
        /// </summary>
        public async Task HalfOpenCircuitAsync(ProcessorType processorType)
        {
            await _database.StringSetAsync(GetStateKey(processorType), "Half-Open");
        }
    }
}
