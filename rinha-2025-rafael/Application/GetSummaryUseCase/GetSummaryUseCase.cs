using rinha_2025_rafael.Domain;
using rinha_2025_rafael.Infrastructure.Cache;

namespace rinha_2025_rafael.Application.GetSummaryUseCase
{
    public class GetSummaryUseCase : IGetSummaryUseCase
    {
        private readonly IRedisService _redisService;

        public GetSummaryUseCase(IRedisService redisService)
        {
            _redisService = redisService;
        }

        public async Task<PaymentSummaryResponse> ExecuteAsync(DateTime? from, DateTime? to)
        {
            return await _redisService.GetSummaryAsync(from, to);
        }
    }
}
