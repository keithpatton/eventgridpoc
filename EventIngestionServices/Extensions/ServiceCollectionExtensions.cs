using EventIngestionServices.Abstractions;
using EventIngestionServices.Options;
using Microsoft.Extensions.DependencyInjection;

namespace EventIngestionServices.Extensions
{
    /// <summary>
    /// Contains extension methods for IServiceCollection to add event ingestion services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds event ingestion services to the specified IServiceCollection.
        /// </summary>
        /// <typeparam name="T">The type of the event ingestion service implementation. Must implement IEventIngestionService.</typeparam>
        /// <param name="services">The IServiceCollection to add services to.</param>
        /// <param name="eventsIngestionHostedServiceOptions">An optional action to configure the EventsIngestionHostedServiceOptions.</param>
        /// <param name="eventGridIngestionServiceOptions">An optional action to configure the EventGridIngestionServiceOptions.</param>
        /// <param name="redisLockServiceOptions">An optional action to configure the RedisLockServiceOptions.</param>
        /// <returns>The IServiceCollection for chaining.</returns>
        /// <remarks>
        /// This extension method registers event ingestion related services including hosted service, 
        /// event grid ingestion service, and Redis lock service. It allows optional configuration for each service 
        /// through provided Action delegates. If no configuration is provided, default settings are used.
        /// </remarks>
        public static IServiceCollection AddEventGridIngestion<T>(
            this IServiceCollection services,
            Action<EventsIngestionHostedServiceOptions>? eventsIngestionHostedServiceOptions = null,
            Action<EventGridIngestionServiceOptions>? eventGridIngestionServiceOptions = null,
            Action<RedisLockServiceOptions>? redisLockServiceOptions = null) where T : class, IEventIngestionService
        {
            // ensure options configured
            services.Configure<EventsIngestionHostedServiceOptions>(opts =>
            {
                eventsIngestionHostedServiceOptions?.Invoke(opts);
            });
            services.Configure<EventGridIngestionServiceOptions>(opts =>
            {
                eventGridIngestionServiceOptions?.Invoke(opts);
            });

            services.Configure<RedisLockServiceOptions>(opts =>
            {
                redisLockServiceOptions?.Invoke(opts);
            });
            // ensure services registered
            services.AddHostedService<EventsIngestionHostedService>();
            services.AddSingleton<IEventsIngestionService, EventGridIngestionService>();
            services.AddSingleton<IEventIngestionService, T>();
            services.AddSingleton<IRedisLockService, RedisLockService>();

            return services;
        }
    }
}
