namespace rinha_2025_rafael.Domain
{
    public record PaymentRequest(Guid CorrelationId, decimal Amount);
}
