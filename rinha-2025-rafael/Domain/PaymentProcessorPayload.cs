namespace rinha_2025_rafael.Domain
{
    public record PaymentProcessorPayload(Guid CorrelationId, decimal Amount, DateTime RequestedAt);
}
