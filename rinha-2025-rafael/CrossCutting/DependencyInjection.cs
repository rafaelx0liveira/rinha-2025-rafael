using rinha_2025_rafael.Application.EnqueuePaymentUseCase;
using rinha_2025_rafael.Application.GetSummaryUseCase;
using rinha_2025_rafael.Infrastructure.Cache;
using rinha_2025_rafael.Workers;
using StackExchange.Redis;

namespace rinha_2025_rafael.CrossCutting
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services
                .AddUseCases()
                .AddRedis(configuration)
                .AddWorker();

            return services;
        }

        private static IServiceCollection AddUseCases(this IServiceCollection services)
        {
            services.AddScoped<IEnqueuePaymentUseCase, EnqueuePaymentUseCase>();
            services.AddScoped<IGetSummaryUseCase, GetSummaryUseCase>();

            return services;
        }

        private static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration["Redis:ConnectionString"] ?? throw new ArgumentNullException("ConnectionString is missing");

            services.AddSingleton<IRedisService, RedisService>();
            services.AddSingleton<IConnectionMultiplexer>(
                sp => ConnectionMultiplexer.Connect(connectionString));

            return services;
        }

        private static IServiceCollection AddWorker(this IServiceCollection services)
        {
            services.AddScoped<PaymentProcessingWorker>();

            return services;
        }
    }
}
