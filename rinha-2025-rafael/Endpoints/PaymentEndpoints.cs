using Microsoft.AspNetCore.Mvc;
using rinha_2025_rafael.Application.EnqueuePaymentUseCase;
using rinha_2025_rafael.Application.GetSummaryUseCase;
using rinha_2025_rafael.Domain;

namespace rinha_2025_rafael.Endpoints
{
    public static class PaymentEndpoints
    {
        public static void MapPaymentsEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/payments"); 

            // Endpoint: POST /payments
            // Recebe e enfileira um pagamento para processamento assíncrono.
            group.MapPost("/", async (
                PaymentRequest request,
                [FromServices] IEnqueuePaymentUseCase _enqueuePaymentUseCase) =>
            {
                await _enqueuePaymentUseCase.ExecuteAsync(request);

                return Results.Accepted();
            })
                .WithName("ProcessPayment")
                .WithDescription("Recebe e enfileira um pagamento para processamento");

            // Endpoint: GET /payments-summary
            // Retorna um resumo dos pagamentos processados.
            app.MapGet("/payments-summary", async (
                [FromServices] IGetSummaryUseCase _getSummaryUseCase,
                [FromQuery] DateTime? from,
                [FromQuery] DateTime? to) =>
            {
                var summary = await _getSummaryUseCase.ExecuteAsync(from, to);

                return Results.Ok(summary);
            })
                .WithName("GetPayments")
                .WithDescription("Retorna um resumo dos pagamentos processados.");
        }
    }
}
