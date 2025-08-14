using rinha_2025_rafael.Domain;
using rinha_2025_rafael.Domain.Enum;

namespace rinha_2025_rafael.Infrastructure.Cache
{
    public interface IRedisService
    {
        Task EnqueuePaymentAsync(PaymentRequest request);
        Task UpdateSummaryAsync(ProcessorType processorType, decimal amount);
        Task<PaymentSummaryResponse> GetSummaryAsync();
    }
}
