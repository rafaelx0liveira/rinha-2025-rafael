using rinha_2025_rafael.Domain.Enum;

namespace rinha_2025_rafael.Infrastructure.Resilience
{
    public interface ICircuitBreakerService
    {
        Task<bool> IsClosedAsync(ProcessorType processorType);
        Task RecordFailureAsync(ProcessorType processorType);
        Task RecordSuccessAsync(ProcessorType processorType);
        Task OpenCircuitAsync(ProcessorType processorType);
        Task HalfOpenCircuitAsync(ProcessorType processorType);
    }
}
