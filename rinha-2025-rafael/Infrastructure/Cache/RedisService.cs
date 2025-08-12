using rinha_2025_rafael.Domain;
using StackExchange.Redis;
using System.Text.Json;

namespace rinha_2025_rafael.Infrastructure.Cache
{
    public class RedisService : IRedisService
    {
        private readonly IDatabase _db;
        private const string PaymentQueueKey = "payments_queue";

        public RedisService(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        public async Task EnqueuePaymentAsync(PaymentRequest request)
        {
            var payload = JsonSerializer.Serialize(request);

            // Adiciona elemento no início (à esquerda) para o Worker pegar o do final (à direita).
            await _db.ListLeftPushAsync(PaymentQueueKey, payload);
        }
    }
}
