using rinha_2025_rafael.Domain.Enum;
using rinha_2025_rafael.Infrastructure.Clients;
using rinha_2025_rafael.Infrastructure.Resilience;

namespace rinha_2025_rafael.Workers
{
    public class HealthCheckSentinel : BackgroundService
    {
        private readonly ILogger<HealthCheckSentinel> _logger;
        private readonly IServiceProvider _serviceProvider;

        private static readonly TimeSpan RATE_LIMIT = TimeSpan.FromSeconds(5);

        public HealthCheckSentinel(ILogger<HealthCheckSentinel> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[HEALTH] - Health Check Sentinel iniciado...");
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _serviceProvider.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<IPaymentProcessorClient>();
                var circuitBreaker = scope.ServiceProvider.GetRequiredService<ICircuitBreakerService>();

                _logger.LogInformation("[HEALTH] - Executando verificação da saúde dos processadores...");

                // Executa as duas verificações em paralelo para otimizar o tempo.
                var defaultCheckTask = CheckProcessorHealthAsync(ProcessorType.DEFAULT, client, circuitBreaker);
                var fallbackCheckTask = CheckProcessorHealthAsync(ProcessorType.FALLBACK, client, circuitBreaker);

                await Task.WhenAll(defaultCheckTask, fallbackCheckTask);

                // Espera 5 segundos para o próximo ciclo, respeitando o rate limit.
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        /// <summary>
        /// Método auxiliar que encapsula a lógica de verificação de saúde para um processador.
        /// </summary>
        private async Task CheckProcessorHealthAsync(
            ProcessorType processorType,
            IPaymentProcessorClient client,
            ICircuitBreakerService circuitBreaker)
        {
            try
            {
                var health = await client.GetHealthAsync(processorType);
                if (health is not null && !health.Failing)
                {
                    // Se o serviço está saudável, registramos o sucesso no Circuit Breaker.
                    await circuitBreaker.RecordSuccessAsync(processorType);
                    _logger.LogInformation($"[HEALTH] - Processador {processorType} está saudável.");
                }
                else
                {
                    // Se a API reporta falha, abrimos o circuito.
                    await circuitBreaker.OpenCircuitAsync(processorType);
                    _logger.LogWarning($"[HEALTH] - Processador {processorType} reportou falha. Abrindo o circuito.");
                }
            }
            catch (Exception ex)
            {
                // Se qualquer exceção ocorrer (timeout, erro de rede, etc.), abrimos o circuito.
                _logger.LogError(ex, $"[HEALTH] - Falha ao verificar saúde do processador {processorType}. Abrindo o circuito.");
                await circuitBreaker.OpenCircuitAsync(processorType);
            }
        }
    }
}
