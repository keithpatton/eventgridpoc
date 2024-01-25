using Microsoft.Extensions.DependencyInjection;
using Serko.Messaging.EventPublishing.Abstractions;
using Serko.Messaging.EventPublishing.Options;
using Serko.Messaging.EventPublishing.Services;

namespace Serko.Messaging.EventPublishing.Extensions
{

    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds services required for publishing events to Azure Event Grid, including the event queue service and the background service for publishing.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
        /// <param name="eventGridPublishingServiceOptions">An optional action to configure the <see cref="EventGridPublishingServiceOptions"/>.</param>
        /// <returns>The original <see cref="IServiceCollection"/> instance with the event grid publishing services added.</returns>
        /// <remarks>
        /// This extension method configures the necessary services for event grid publishing, allowing customization of options through <paramref name="eventGridPublishingServiceOptions"/>.
        /// It registers <see cref="EventGridPublishingService"/> as a hosted service to run in the background and <see cref="EventQueueService"/> as a singleton to manage event queuing.
        /// </remarks>
        public static IServiceCollection AddEventGridPublishing(
            this IServiceCollection services,
            Action<EventGridPublishingServiceOptions>? eventGridPublishingServiceOptions = null)
        {
            if (eventGridPublishingServiceOptions != null)
            {
                services.Configure(eventGridPublishingServiceOptions);
            }
            services.AddHostedService<EventGridPublishingService>();
            services.AddSingleton<IEventQueueService, EventQueueService>();
            return services;
        }
    }
}