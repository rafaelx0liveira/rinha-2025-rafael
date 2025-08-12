using rinha_2025_rafael.Domain;
using StackExchange.Redis;
using System.Text.Json;

namespace rinha_2025_rafael.Workers
{
    public class PaymentProcessingWorker : BackgroundService
    {
        private readonly ILogger<PaymentProcessingWorker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly string _paymentQueueKey;
        private const int DELAY_PROCESSING_AGAIN = 5000;

        public PaymentProcessingWorker(
            ILogger<PaymentProcessingWorker> logger,
            IServiceProvider serviceProvider,
            IConfiguration configuration
        ) : base()
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _paymentQueueKey = configuration["Redis:ConnectionString"] ?? throw new ArgumentNullException("ConnectionString is missing");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Payment Processing Worker iniciado.");

            while (!stoppingToken.IsCancellationRequested) 
            {
                using var scope = _serviceProvider.CreateScope();
                var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>().GetDatabase();

                try
                {
                    var redisValue = await redis.ListRightPopAsync(_paymentQueueKey);

                    if (redisValue.HasValue)
                    {
                        var paymentRequest = JsonSerializer.Deserialize<PaymentRequest>(redisValue!);
                        if (paymentRequest != null)
                        {
                            _logger.LogInformation("Processando pagamento: CorrelationId = {CorrelationId}, Amount = {Amount}",
                            paymentRequest.CorrelationId, paymentRequest.Amount);

                            // TODO: Lógica do Circuit Breaker
                        }
                    }
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Erro ao processar pagamento da fila");
                    await Task.Delay(DELAY_PROCESSING_AGAIN, stoppingToken);
                }
            }
        }
    }
}
