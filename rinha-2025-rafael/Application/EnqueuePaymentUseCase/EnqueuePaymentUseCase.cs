using rinha_2025_rafael.Domain;
using rinha_2025_rafael.Infrastructure.Cache;

namespace rinha_2025_rafael.Application.EnqueuePaymentUseCase
{
    public class EnqueuePaymentUseCase (
            RedisService redisService
        ) : IEnqueuePaymentUseCase
    {
        private readonly RedisService _redisService = redisService;

        public async Task ExecuteAsync(PaymentRequest request)
        {
            await _redisService.EnqueuePaymentAsync(request);
        }
    }
}
