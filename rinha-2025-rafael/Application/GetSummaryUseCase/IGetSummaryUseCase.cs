using rinha_2025_rafael.Domain;

namespace rinha_2025_rafael.Application.GetSummaryUseCase
{
    public interface IGetSummaryUseCase
    {
        Task<PaymentSummaryResponse> ExecuteAsync(DateTime? from, DateTime? to);
    }
}
