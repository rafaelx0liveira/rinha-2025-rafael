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
        private readonly IServiceProvider _serviceProvider;
        private const int DELAY_PROCESSING_AGAIN = 5000;

        // Grau de paralelismo
        private readonly SemaphoreSlim _semaphore = new(20);

        public PaymentProcessingWorker(
            ILogger<PaymentProcessingWorker> logger,
            IServiceProvider serviceProvider
        ) : base()
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[WORKER] - Payment Processing Worker (Concorrente) iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>().GetDatabase();

                    // Recupera um item da fila do Redis
                    var redisValue = await redis.ListRightPopAsync(_paymentQueueKey);

                    if (redisValue.HasValue)
                    {
                        // Se encontrarmos um item
                        // Aguarda por um "assistente" livre
                        await _semaphore.WaitAsync(stoppingToken);

                        // Entrega o trabalho para o assistente e não espera por ele
                        // O _ significa "fire and forget"
                        _ = ProcessPaymentIndependentlyAsync(redisValue, stoppingToken);
                    }
                    else
                    {
                        // Se a fila estiver vazia, faz uma pausa para não sobrecarregar a CPU
                        // Ex.: 50 ms
                        await Task.Delay(50, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[WORKER] - Erro ao processar pagamento da fila");
                    await Task.Delay(DELAY_PROCESSING_AGAIN, stoppingToken);
                }
            }
        }

        /// <summary>
        /// Este método é executado por cada "assistente" de forma independente.
        /// </summary>
        private async Task ProcessPaymentIndependentlyAsync(RedisValue redisValue, CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var circuitBreaker = scope.ServiceProvider.GetRequiredService<ICircuitBreakerService>();
            var client = scope.ServiceProvider.GetRequiredService<IPaymentProcessorClient>();
            var redisService = scope.ServiceProvider.GetRequiredService<IRedisService>();
            var jsonOptions = scope.ServiceProvider.GetRequiredService<JsonSerializerOptions>();

            var paymentRequest = JsonSerializer.Deserialize<PaymentRequest>(redisValue!, jsonOptions);
            if (paymentRequest is null) return;

            try
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

                        // Usando o '_' para indicar "fire and forget"
                        // O Worker dispara a atualização do Redis mas não espera por ela
                        // Encurtando assim a janela de inconsistências
                        _ = redisService.RecordPaymentAsync(ProcessorType.DEFAULT, paymentRequest, DateTime.UtcNow);
                        processed = true;
                    }
                    catch (Exception ex)
                    {
                        // Se falhar:
                        _logger.LogError(ex, $"[WORKER] - Falha ao processar pagamento {paymentRequest.CorrelationId} pelo Default");
                        await circuitBreaker.RecordFailureAsync(ProcessorType.DEFAULT);
                    }
                }

                // 2. Se não foi processado, tenta o fallback
                if (!processed && await circuitBreaker.IsClosedAsync(ProcessorType.FALLBACK))
                {
                    try
                    {
                        await client.ProcessPaymentAsync(paymentRequest, ProcessorType.FALLBACK);

                        await circuitBreaker.RecordSuccessAsync(ProcessorType.FALLBACK);

                        _logger.LogInformation($"[WORKER] - Pagamento {paymentRequest.CorrelationId} processado com sucesso pelo Fallback.");

                        // Usando o '_' para indicar "fire and forget"
                        // O Worker dispara a atualização do Redis mas não espera por ela
                        // Encurtando assim a janela de inconsistências
                        _ = redisService.RecordPaymentAsync(ProcessorType.FALLBACK, paymentRequest, DateTime.UtcNow);
                        processed = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"[WORKER] - Falha ao processar pagamento {paymentRequest.CorrelationId} pelo Fallback.");
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WORKER] - Erro ao processar pagamento da fila");
                await Task.Delay(DELAY_PROCESSING_AGAIN, stoppingToken);
            }
            finally
            {
                // Libera o "assistente" para pegar um novo trabalho.
                _semaphore.Release();
            }
        }
    }
}
