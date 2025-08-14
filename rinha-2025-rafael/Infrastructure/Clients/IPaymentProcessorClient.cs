using rinha_2025_rafael.Domain;
using rinha_2025_rafael.Domain.Enum;

namespace rinha_2025_rafael.Infrastructure.Clients
{
    public interface IPaymentProcessorClient
    {
        Task ProcessPaymentAsync(PaymentRequest request, ProcessorType processorType);
        Task<HealthCheckResponse?> GetHealthAsync(ProcessorType processorType); 
    }
}
