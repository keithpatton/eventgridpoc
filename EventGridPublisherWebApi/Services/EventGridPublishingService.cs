using Azure;
using Azure.Identity;
using Azure.Messaging.EventGrid.Namespaces;
using EventGridPublisherWebApi.Abstractions;
using EventGridPublisherWebApi.Options;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace EventGridPublisherWebApi.Services
{
    /// <summary>
    /// A background service that periodically dequeues events from the event queue service and publishes them to Azure Event Grid.
    /// </summary>
    public class EventGridPublishingService : BackgroundService
    {
        private readonly IEventQueueService _eventQueueService;
        private readonly ILogger<EventGridPublishingService> _logger;
        private readonly ConcurrentDictionary<string, EventGridClient> _clients = new();
        private readonly EventGridPublishingServiceOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventGridPublishingService"/> class.
        /// </summary>
        /// <param name="eventQueueService">The service for event queue management.</param>
        /// <param name="optionsAccessor">Configuration options for event grid publishing.</param>
        /// <param name="logger">Logger for capturing runtime information.</param>
        public EventGridPublishingService(IEventQueueService eventQueueService,
            IOptions<EventGridPublishingServiceOptions> optionsAccessor,ILogger<EventGridPublishingService> logger)
        {
            _eventQueueService = eventQueueService;
            _options = optionsAccessor.Value;
            _logger = logger;
        }

        /// <summary>
        /// Executes the background task to dequeue and publish events to Azure Event Grid.
        /// </summary>
        /// <param name="stoppingToken">Cancellation token that can be used to stop the background task.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("EventGridPublishingService is starting.");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var publishTasks = _options.Topics
                        .Select(t => t.Name)
                        .Select(PublishTopicEvents)
                        .ToList();
                    await Task.WhenAll(publishTasks);
                    _logger.LogDebug("Waiting for {PublishingInterval} seconds before attempting to find and publish events", _options.PublishingInterval.TotalSeconds);
                    await Task.Delay(_options.PublishingInterval, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred in the event publishing loop.");
                }
            }
            _logger.LogDebug("EventGridPublishingService is stopping.");
        }

        /// <summary>
        /// Dequeues events for a specific topic and publishes them to Azure Event Grid.
        /// </summary>
        /// <param name="topicName">The name of the topic for which to publish events.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous publish operation.</returns>
        private async Task PublishTopicEvents(string topicName)
        {
            try
            {
                var topicEvents = _eventQueueService.DequeueEvents(topicName, _options.EventBatchSize).ToList();
                if (topicEvents.Any())
                {
                    var client = GetOrCreateEventGridClient(topicName);
                    var cloudEvents = topicEvents.Select(eqi => eqi.CloudEvent).ToList();
                    await client.PublishCloudEventsAsync(topicName, cloudEvents);
                    _logger.LogDebug("{Count} events published for topic '{TopicName}'.", topicEvents.Count, topicName);
                }
                else
                {
                    _logger.LogDebug("No events found for topic '{TopicName}'.", topicName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish events for topic '{TopicName}'.", topicName);
                // TODO Consideration for requeuing or handling failed events could be added here.
            }
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
        private EventGridClient GetOrCreateEventGridClient(string topicName)
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