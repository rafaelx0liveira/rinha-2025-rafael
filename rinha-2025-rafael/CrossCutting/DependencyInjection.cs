using rinha_2025_rafael.Application.EnqueuePaymentUseCase;
using rinha_2025_rafael.Application.GetSummaryUseCase;
using rinha_2025_rafael.Infrastructure.Cache;
using rinha_2025_rafael.Infrastructure.Clients;
using rinha_2025_rafael.Infrastructure.Resilience;
using rinha_2025_rafael.Workers;
using StackExchange.Redis;
using System.Text.Json;

namespace rinha_2025_rafael.CrossCutting
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {

            services
                .AddJsonSerialization()
                .AddUseCases()
                .AddRedis(configuration)
                .AddWorker()
                .AddCircuitBreaker()
                .AddHealthCheckSentinel()
                .AddClients();

            return services;
        }

        private static IServiceCollection AddJsonSerialization(this IServiceCollection services)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                TypeInfoResolver = JsonContext.Default,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            services.AddSingleton(jsonOptions);

            services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.TypeInfoResolver = jsonOptions.TypeInfoResolver;
                options.SerializerOptions.PropertyNamingPolicy = jsonOptions.PropertyNamingPolicy;
            });

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
            var connectionString = configuration["Redis:ConnectionString"] ?? throw new ArgumentNullException("Redis ConnectionString is missing");

            services.AddSingleton<IRedisService, RedisService>();
            services.AddSingleton<IConnectionMultiplexer>(
                sp => ConnectionMultiplexer.Connect(connectionString));

            return services;
        }

        private static IServiceCollection AddWorker(this IServiceCollection services)
        {
            services.AddHostedService<PaymentProcessingWorker>();

            return services;
        }

        private static IServiceCollection AddCircuitBreaker(this IServiceCollection services)
        {
            services.AddSingleton<ICircuitBreakerService, CircuitBreakerService>();

            return services;
        }

        private static IServiceCollection AddHealthCheckSentinel(this IServiceCollection services)
        {
            services.AddHostedService<HealthCheckSentinel>();

            return services;
        }

        private static IServiceCollection AddClients(this IServiceCollection services)
        {
            services.AddHttpClient<IPaymentProcessorClient, PaymentProcessorClient>(client =>
            {
                // Se uma requisição demorar mais de 10s, ela será cancelada
                client.Timeout = TimeSpan.FromSeconds(60);
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
                {
                    return new HttpClientHandler
                    {
                        // Permite que o cliente se conecte a serviços HTTP em ves de HTTPS, por conta da comunicação interna entre containers.
                        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    };
                });

            return services;
        }
    }
}
