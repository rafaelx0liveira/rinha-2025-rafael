using rinha_2025_rafael.Domain.Enum;
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

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Health Check Sentinel iniciado...");

            // Aguardando a aplicação iniciar
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

            while (!cancellationToken.IsCancellationRequested) 
            {
                using var scope = _serviceProvider.CreateScope();
                var circuitBreaker = scope.ServiceProvider.GetRequiredService<ICircuitBreakerService>();

                // TODO: Injetar o HttpClient para fazer as chamadas de health-check

                _logger.LogInformation("Executando verificação da saúde dos processadores...");

                try
                {
                    // TODO: Chamar GET http://payment-processor-default:8080/payments/service-health
                    // Se a resposta for 200 OK e "failing": false, chamar circuitBreaker.RecordSuccessAsync(ProcessorType.Default)
                    // Se a resposta for 200 OK e "failing": true, chamar circuitBreaker.OpenCircuitAsync(ProcessorType.Default)
                    // Se a resposta for erro (5xx, timeout), chamar circuitBreaker.OpenCircuitAsync(ProcessorType.Default)
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Falha ao verificar saúde do processador Default. Abrindo o circuito.");
                    await circuitBreaker.OpenCircuitAsync(ProcessorType.DEFAULT);
                }

                // TODO: Fazer a mesma lógica para o processador Fallback

                // Espera 5 segundos para respeitar o rate limit.
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }
}
