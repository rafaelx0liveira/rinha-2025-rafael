using rinha_2025_rafael.CrossCutting;
using rinha_2025_rafael.Domain;
using rinha_2025_rafael.Domain.Enum;
using rinha_2025_rafael.Infrastructure.Cache;
using rinha_2025_rafael.Infrastructure.Clients;
using rinha_2025_rafael.Infrastructure.Resilience;
using StackExchange.Redis;
using System.Text.Json;

namespace rinha_2025_rafael.Workers
{
    public class PaymentProcessingWorker : BackgroundService
    {
        private readonly string _paymentQueueKey = "payments_queue";
        private readonly ILogger<PaymentProcessingWorker> _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IServiceProvider _serviceProvider;
        private const int DELAY_PROCESSING_AGAIN = 5000;

        public PaymentProcessingWorker(
            ILogger<PaymentProcessingWorker> logger,
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            JsonSerializerOptions options
        ) : base()
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _jsonOptions = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[WORKER] - Payment Processing Worker iniciado.");

            while (!stoppingToken.IsCancellationRequested) 
            {
                using var scope = _serviceProvider.CreateScope();
                var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
                var circuitBreaker = scope.ServiceProvider.GetRequiredService<ICircuitBreakerService>();
                var client = scope.ServiceProvider.GetRequiredService<IPaymentProcessorClient>();
                var redisService = scope.ServiceProvider.GetRequiredService<IRedisService>();

                try
                {
                    var redisValue = await redis.ListRightPopAsync(_paymentQueueKey);

                    if (redisValue.HasValue)
                    {
                        var paymentRequest = JsonSerializer.Deserialize<PaymentRequest>(redisValue!, _jsonOptions);

                        if (paymentRequest != null)
                        {
                            _logger.LogInformation($"[WORKER] - Processando pagamento: CorrelationId = {paymentRequest.CorrelationId}, Amount = {paymentRequest.Amount}",
                            paymentRequest.CorrelationId, paymentRequest.Amount);

                            bool processed = false;

                            // 1. Tenta processar o pagamento via DEFAULT
                            if (await circuitBreaker.IsClosedAsync(ProcessorType.DEFAULT))
                            {
                                try
                                {
                                    await client.ProcessPaymentAsync(paymentRequest, ProcessorType.DEFAULT);

                                    await circuitBreaker.RecordSuccessAsync(ProcessorType.DEFAULT);

                                    _logger.LogInformation($"[WORKER] - Pagamento {paymentRequest.CorrelationId} processado com sucesso pelo Default.");

                                    await redisService.UpdateSummaryAsync(ProcessorType.DEFAULT, paymentRequest.Amount);
                                    processed = true;
                                }
                                catch (Exception ex)
                                {
                                    // Se falhar:
                                    _logger.LogError($"[WORKER] - Falha ao processar pagamento {paymentRequest.CorrelationId} pelo Default. Erro: {ex}");
                                    await circuitBreaker.RecordFailureAsync(ProcessorType.DEFAULT);
                                }
                            }
                            
                            // 2. Se não foi processado, tenta o fallback
                            if(!processed && await circuitBreaker.IsClosedAsync(ProcessorType.FALLBACK))
                            {
                                try
                                {
                                    await client.ProcessPaymentAsync(paymentRequest, ProcessorType.FALLBACK);

                                    await circuitBreaker.RecordSuccessAsync(ProcessorType.FALLBACK);

                                    _logger.LogInformation($"[WORKER] - Pagamento {paymentRequest.CorrelationId} processado com sucesso pelo Fallback.");

                                    await redisService.UpdateSummaryAsync(ProcessorType.FALLBACK, paymentRequest.Amount);
                                    processed = true;
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError($"[WORKER] - Falha ao processar pagamento {paymentRequest.CorrelationId} pelo Fallback. Erro: {ex}");
                                    await circuitBreaker.RecordFailureAsync(ProcessorType.FALLBACK);
                                }
                            }

                            // 3. Se ainda não foi procesado (ou seja, ambos falharam) reenfileira o pagamento
                            if (!processed)
                            {
                                _logger.LogWarning($"[WORKER] - Ambos os processadores indisponíveis. Reenfileirando pagamento {paymentRequest.CorrelationId}");

                                await redisService.RequeuePaymentAsync(paymentRequest);

                                await Task.Delay(100, stoppingToken);
                            }
                        }
                    }
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "[WORKER] - Erro ao processar pagamento da fila");
                    await Task.Delay(DELAY_PROCESSING_AGAIN, stoppingToken);
                }
            }
        }
    }
}
