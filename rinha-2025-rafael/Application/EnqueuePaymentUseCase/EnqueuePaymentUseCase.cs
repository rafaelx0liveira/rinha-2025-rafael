using rinha_2025_rafael.Domain;
using rinha_2025_rafael.Infrastructure.Cache;

namespace rinha_2025_rafael.Application.EnqueuePaymentUseCase
{
    public class EnqueuePaymentUseCase (
            IRedisService redisService
        ) : IEnqueuePaymentUseCase
    {
        private readonly IRedisService _redisService = redisService;

        public async Task ExecuteAsync(PaymentRequest request)
        {
            await _redisService.EnqueuePaymentAsync(request);
        }
    }
}
