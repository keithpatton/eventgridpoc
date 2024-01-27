using Azure;
using Azure.Identity;
using Azure.Messaging;
using Azure.Messaging.EventGrid.Namespaces;
using EventGridPublisherWebApi.Options;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using Wolverine.Attributes;

namespace EventGridPublisherWebApi.Handlers
{

    /// <summary>
    /// Represents a business event that has been fired and contains the CloudEvent data and the topic name to which it belongs.
    /// </summary>
    public record BusinessEventFired(CloudEvent CloudEvent, string TopicName);


    /// <summary>
    /// Handles business events by publishing them to the specified Azure Event Grid topic.
    /// </summary>
    /// <remarks>
    /// This handler includes automatic retry logic for handling exceptions that occur during event publishing,
    /// optimizing resource usage by caching EventGridClient instances for reuse.
    /// </remarks>
    [RetryNow(typeof(Exception), 50, 100, 250)]
    public class BusinessEventFiredHandler
    {
        private readonly ILogger<BusinessEventFiredHandler> _logger;
        private static readonly ConcurrentDictionary<string, EventGridClient> _clients = new();
        private readonly EventGridPublishingOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessEventFiredHandler"/> class.
        /// </summary>
        /// <param name="optionsAccessor">Provides access to the event grid publishing options.</param>
        /// <param name="logger">The logger for logging information, warnings, and errors.</param>
        public BusinessEventFiredHandler(IOptions<EventGridPublishingOptions> optionsAccessor, ILogger<BusinessEventFiredHandler> logger)
        {
            _options = optionsAccessor.Value;
            _logger = logger;
        }

        /// <summary>
        /// Handles the publishing of a business event to Azure Event Grid.
        /// </summary>
        /// <param name="businessEventFired">The business event containing the CloudEvent and topic name.</param>
        /// <returns>A task that represents the asynchronous operation of publishing an event.</returns>
        /// <remarks>
        /// This method publishes the provided CloudEvent to the Azure Event Grid topic specified in the event data.
        /// It uses a cached EventGridClient for the topic if available, or creates a new one if not.
        /// </remarks>
        public async Task Handle(BusinessEventFired businessEventFired)
        {
            var client = GetOrCreateEventGridClient(businessEventFired.TopicName);
            await client.PublishCloudEventAsync(businessEventFired.TopicName, businessEventFired.CloudEvent);
            _logger.LogDebug("Event {EventId} Successfully Published to Event Grid Topic {TopicName}", businessEventFired.CloudEvent.Id, businessEventFired.TopicName);
        }

        /// <summary>
        /// Retrieves an existing EventGridClient instance from the cache or creates a new one if it doesn't exist.
        /// </summary>
        /// <param name="topicName">The name of the topic associated with the EventGridClient.</param>
        /// <returns>
        /// An EventGridClient instance configured for the specified topic and subscription.
        /// </returns>
        /// <remarks>
        /// This method ensures that each unique combination of topic name and subscription 
        /// uses the same EventGridClient instance, optimizing resource usage.
        /// </remarks>
        private EventGridClient GetOrCreateEventGridClient(string topicName)
        {
            return _clients.GetOrAdd(topicName, _ =>
            {
                return CreateEventGridClient(topicName);
            });
        }

        /// <summary>
        /// Creates an EventGridClient instance for interacting with Azure Event Grid.
        /// </summary>
        /// <param name="topicName">The name of the Event Grid topic.</param>
        /// <returns>
        /// An EventGridClient instance configured with the appropriate credentials. If a topic key is available,
        /// an AzureKeyCredential is used for authentication. Otherwise, DefaultAzureCredential is used, 
        /// which can handle various authentication scenarios (like Managed Identity or development environment credentials).
        /// </returns>
        /// <remarks>
        /// This method abstracts the creation of the EventGridClient, handling different authentication methods based on
        /// the presence of a topicKey. It allows seamless switching between key-based authentication for scenarios where
        /// the key is available and Azure AD-based authentication (using DefaultAzureCredential) in environments like Azure
        /// where Managed Identities can be leveraged.
        /// </remarks>
        private EventGridClient CreateEventGridClient(string topicName)
        {
            EventGridClient eventGridClient;
            var topicConfig = _options.Topics.FirstOrDefault(t => t.Name == topicName);
            if (topicConfig != null && !string.IsNullOrWhiteSpace(topicConfig.Key))
            {
                eventGridClient = new EventGridClient(new Uri(_options.NamespaceEndpoint), new AzureKeyCredential(topicConfig.Key));
            }
            else
            {
                eventGridClient = new EventGridClient(new Uri(_options.NamespaceEndpoint), new DefaultAzureCredential());
            }
            return eventGridClient;
        }

    }

}
