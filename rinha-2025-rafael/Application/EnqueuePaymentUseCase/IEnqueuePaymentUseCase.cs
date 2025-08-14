using rinha_2025_rafael.Domain;

namespace rinha_2025_rafael.Application.EnqueuePaymentUseCase
{
    public interface IEnqueuePaymentUseCase
    {
        Task ExecuteAsync(PaymentRequest request);
    }
}
