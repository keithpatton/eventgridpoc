using Azure;
using Azure.Identity;
using Azure.Messaging;
using Azure.Messaging.EventGrid.Namespaces;
using EventGridIngestionServices.Abstractions;
using EventGridIngestionServices.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventGridIngestionServices
{

    /// <summary>
    /// Service responsible for ingesting events from Azure Event Grid.
    /// </summary>
    public class EventGridIngestionService : IEventsIngestionService
    {
        private readonly EventsIngestionServiceOptions _options;
        private readonly ILogger _logger;
        private readonly IEventIngestionService _eventIngestionService;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventsIngestionService"/> class.
        /// </summary>
        /// <param name="loggerFactory">The factory to create an instance of <see cref="ILogger"/>.</param>
        /// <param name="optionsAccessor">The configuration options for the service.</param>
        /// <param name="eventIngestionService">The service to process each individual event.</param>
        public EventGridIngestionService(ILoggerFactory loggerFactory,
            IOptions<EventsIngestionServiceOptions> optionsAccessor, IEventIngestionService eventIngestionService)
        {
            _options = optionsAccessor.Value;
            _logger = loggerFactory.CreateLogger<EventGridIngestionService>();
            _eventIngestionService = eventIngestionService;
        }

        /// <summary>
        /// Asynchronously ingests events from configured topics.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation of ingesting events.</returns>
        public async Task IngestAsync()
        {
            var topicConfigs = _options.Topics;
            var tasks = topicConfigs.SelectMany(topic =>
                topic.Subscriptions.Select(subscription =>
                    IngestTopicEventsAsync(topic.Name, subscription, topic.Key))).ToList();
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Ingests events for a specific topic and subscription
        /// </summary>
        /// <param name="topicName">The name of the topic to ingest events from.</param>
        /// <param name="subscription">The name of the subscription to ingest events from.</param>
        /// <param name="topicKey">The access key for the topic.</param>
        /// <returns>A task that represents the asynchronous operation of ingesting events for a topic.</returns>
        private async Task IngestTopicEventsAsync(string topicName, string subscription, string topicKey)
        {
            try
            {
                var eventGridClient = CreateEventGridClient(topicKey);
                bool eventsToIngest;
                do
                {
                    var maxWaitTime = _options.MaxWaitTime;
                    _logger.LogInformation($"Events requested for {topicName}");
                    ReceiveResult result = await eventGridClient.ReceiveCloudEventsAsync(topicName, subscription, _options.EventBatchSize, maxWaitTime);

                    eventsToIngest = result.Value.Any();
                    if (eventsToIngest)
                    {
                        _logger.LogInformation($"{result.Value.Count} Events received for {topicName}");
                        var ingestionTasks = result.Value.Select(detail => IngestEventAsync(eventGridClient, topicName, subscription, detail)).ToList();
                        await Task.WhenAll(ingestionTasks);
                        _logger.LogInformation($"{result.Value.Count} Events ingested for {topicName}");
                    }
                    else
                    {
                        _logger.LogInformation($"No Events received for {topicName}");
                    }
                } while (eventsToIngest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred when ingesting topic events for {topicName}");
                throw;
            }
        }

        /// <summary>
        /// Ingests an individual event.
        /// </summary>
        /// <param name="eventgridClient">The EventGridClient to use for acknowledging the event.</param>
        /// <param name="topicName">The name of the topic from which the event originates.</param>
        /// <param name="detail">The details of the event to ingest.</param>
        /// <param name="detail">The name of the subscription from which the event was received.</param>
        /// <returns>A task that represents the asynchronous operation of ingesting an individual event.</returns>
        /// <remarks>
        /// Ensure idempotency in this method, as the same events might be delivered multiple times or in an unexpected order.
        /// </remarks>
        private async Task IngestEventAsync(EventGridClient eventgridClient, string topicName, string subscription, ReceiveDetails detail)
        {
            CloudEvent cloudEvent = detail.Event;
            BrokerProperties brokerProperties = detail.BrokerProperties;
            try
            {
                await _eventIngestionService.IngestAsync(cloudEvent);
                AcknowledgeResult acknowlegeResult = await eventgridClient.AcknowledgeCloudEventsAsync(topicName, subscription, new AcknowledgeOptions(new List<string> { brokerProperties.LockToken }));
                LogLockTokensResult(topicName, acknowlegeResult.SucceededLockTokens, acknowlegeResult.FailedLockTokens);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error ingesting event: {ex.Message}");
                ReleaseResult releaseResult = await eventgridClient.ReleaseCloudEventsAsync(topicName, subscription, new ReleaseOptions(new List<string> { brokerProperties.LockToken }));
                LogLockTokensResult(topicName, releaseResult.SucceededLockTokens, releaseResult.FailedLockTokens);
            }
        }

        /// <summary>
        /// Logs the result summary for lock tokens.
        /// </summary>
        /// <param name="tokenName">The name associated with the lock tokens.</param>
        /// <param name="succeededLockTokens">A list of lock tokens that succeeded.</param>
        /// <param name="failedLockTokens">A list of lock tokens that failed.</param>
        private void LogLockTokensResult(string tokenName, IReadOnlyList<string> succeededLockTokens, IReadOnlyList<FailedLockToken> failedLockTokens)
        {
            if (failedLockTokens.Count > 0)
            {
                _logger.LogWarning($"{tokenName} Failed lock token count: {failedLockTokens.Count}");
                foreach (FailedLockToken failedLockToken in failedLockTokens)
                {
                    _logger.LogWarning($"{tokenName} Lock Token: {failedLockToken.LockToken}");
                    _logger.LogWarning($"{tokenName} Error Code: {failedLockToken.Error}");
                    _logger.LogWarning($"{tokenName} Error Description: {failedLockToken.ToString}");
                }
            }

            _logger.LogDebug($"{tokenName} Success lock token count: {succeededLockTokens.Count}");
            foreach (string lockToken in succeededLockTokens)
            {
                _logger.LogDebug($"{tokenName} Lock Token: {lockToken}");
            }
        }

        /// <summary>
        /// Creates an EventGridClient instance for interacting with Azure Event Grid.
        /// </summary>
        /// <param name="topicKey">The access key for the Event Grid topic. If null, DefaultAzureCredential is used for authentication.</param>
        /// <returns>
        /// An EventGridClient instance configured with the appropriate credentials. If a topicKey is provided,
        /// an AzureKeyCredential is used for authentication. Otherwise, DefaultAzureCredential is used, 
        /// which can handle various authentication scenarios (like Managed Identity or development environment credentials).
        /// </returns>
        /// <remarks>
        /// This method abstracts the creation of the EventGridClient, handling different authentication methods based on
        /// the presence of a topicKey. It allows seamless switching between key-based authentication for scenarios where
        /// the key is available and Azure AD-based authentication (using DefaultAzureCredential) in environments like Azure
        /// where Managed Identities can be leveraged.
        /// </remarks>

        private EventGridClient CreateEventGridClient(string topicKey)
        {
            EventGridClient eventGridClient;
            if (topicKey != null)
            {
                eventGridClient = new EventGridClient(new Uri(_options.NamespaceEndpoint), new AzureKeyCredential(topicKey));
            }
            else
            {
                eventGridClient = new EventGridClient(new Uri(_options.NamespaceEndpoint), new DefaultAzureCredential());
            }
            return eventGridClient;
        }

    }
}
