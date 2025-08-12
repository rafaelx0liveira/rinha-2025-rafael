using rinha_2025_rafael.Domain;

namespace rinha_2025_rafael.Infrastructure.Cache
{
    public interface IRedisService
    {
        Task EnqueuePaymentAsync(PaymentRequest request);
    }
}
