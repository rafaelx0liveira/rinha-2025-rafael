using rinha_2025_rafael.Domain;
using rinha_2025_rafael.Domain.Enum;

namespace rinha_2025_rafael.Infrastructure.Cache
{
    public interface IRedisService
    {
        Task EnqueuePaymentAsync(PaymentRequest request);
        Task RequeuePaymentAsync(PaymentRequest paymentRequest);
        Task RecordPaymentAsync(ProcessorType processorType, PaymentRequest request, DateTime timestamp);
        Task<PaymentSummaryResponse> GetSummaryAsync(DateTime? from, DateTime? to);
    }
}
