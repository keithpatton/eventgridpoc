﻿using Azure;
using Azure.Identity;
using Azure.Messaging.EventGrid.Namespaces;
using EventIngestionServices.Abstractions;
using EventIngestionServices.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace EventIngestionServices
{

    /// <summary>
    /// Service responsible for ingesting events from Azure Event Grid.
    /// </summary>
    public class EventGridIngestionService : IEventsIngestionService
    {
        private readonly EventGridIngestionServiceOptions _options;
        private readonly ILogger _logger;
        private readonly IEventIngestionService _eventIngestionService;
        private readonly ConcurrentDictionary<string, EventGridClient> _clients;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventGridIngestionService"/> class.
        /// </summary>
        /// <param name="loggerFactory">The factory to create an instance of <see cref="ILogger"/>.</param>
        /// <param name="optionsAccessor">The configuration options for the service.</param>
        /// <param name="eventIngestionService">The service to process each individual event.</param>
        /// <remarks>
        /// This service is responsible for ingesting events from Azure Event Grid 
        /// and utilizes a ConcurrentDictionary to manage EventGridClient instances.
        /// </remarks>
        public EventGridIngestionService(ILoggerFactory loggerFactory,
          IOptions<EventGridIngestionServiceOptions> optionsAccessor,
          IEventIngestionService eventIngestionService)
        {
            _options = optionsAccessor.Value;
            _logger = loggerFactory.CreateLogger<EventGridIngestionService>();
            _eventIngestionService = eventIngestionService;
            _clients = new ConcurrentDictionary<string, EventGridClient>();
        }

        /// <summary>
        /// Asynchronously ingests events from configured topics.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation of ingesting events.</returns>
        public async Task IngestAsync()
        {
            var topicSubscriptionIngestionTasks = _options.Topics.SelectMany(topic => topic.Subscriptions.Select(subscription =>
                IngestTopicEventsAsync(topic.Name, subscription, topic.Key))).ToList();
            await Task.WhenAll(topicSubscriptionIngestionTasks);
        }

        /// <summary>
        /// Ingests events for a specific Azure Event Grid topic and subscription.
        /// </summary>
        /// <param name="topicName">The name of the topic to ingest events from.</param>
        /// <param name="subscription">The name of the subscription to ingest events from.</param>
        /// <param name="topicKey">The access key for the topic.</param>
        /// <returns>A task representing the asynchronous operation of ingesting events for a topi and subscription.</returns>
        /// <remarks>
        /// Ingests events from a given topic and subscription, acknowledging successful ingestions and releasing failed ones.
        /// </remarks>
        private async Task IngestTopicEventsAsync(string topicName, string subscription, string topicKey)
        {
            try
            {
                var eventGridClient = GetOrCreateEventGridClient(topicName, topicKey);
                bool eventsToIngest;
                do
                {
                    var maxWaitTime = _options.MaxWaitTime;
                    _logger.LogInformation("Events requested for {TopicName}", topicName);
                    ReceiveResult result = await eventGridClient.ReceiveCloudEventsAsync(topicName, subscription, _options.EventBatchSize, maxWaitTime);
                    eventsToIngest = result.Value.Any();
                    _logger.LogInformation("{EventCount} Events received for {TopicName}", result.Value.Count, topicName);
                    if (eventsToIngest)
                    {
                        var eventIngestionTasks = result.Value.Select(IngestEventAsync);
                        var eventIngestionResults = await Task.WhenAll(eventIngestionTasks);
                        await ProcessEventIngestionResults(eventGridClient, topicName, subscription, eventIngestionResults);
                    }
                } while (eventsToIngest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred when ingesting topic events for {TopicName}", topicName);
                throw;
            }
        }

        /// <summary>
        /// Ingests an individual event.
        /// </summary>
        /// <param name="detail">The details of the event to ingest.</param>
        /// <returns>A task representing the asynchronous operation of ingesting an individual event.</returns>
        /// <remarks>
        /// Processes a single event, handling any exceptions that occur during ingestion.
        /// Idempotency required as the same event might be delivered multiple times.
        /// </remarks>
        private async Task<EventIngestionResult> IngestEventAsync(ReceiveDetails detail)
        {
            try
            {
                await _eventIngestionService.IngestAsync(cloudEvent: detail.Event);
                return new EventIngestionResult(true, detail.BrokerProperties.LockToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ingesting event");
                return new EventIngestionResult(false, detail.BrokerProperties.LockToken);
            }
        }

        /// <summary>
        /// Processes the results of event ingestion, acknowledging successful ingestions and releasing failed ones.
        /// </summary>
        /// <param name="eventGridClient">The EventGridClient used for acknowledging or releasing events.</param>
        /// <param name="topicName">The name of the topic associated with the events.</param>
        /// <param name="subscription">The name of the subscription associated with the events.</param>
        /// <param name="eventIngestionResults">The results of the event ingestion attempts.</param>
        /// <returns>A task representing the asynchronous operation of processing the ingestion results.</returns>
        private async Task ProcessEventIngestionResults(EventGridClient eventGridClient, string topicName, string subscription, EventIngestionResult[] eventIngestionResults)
        {
            var successTokens = eventIngestionResults.Where(result => result.Success).Select(result => result.LockToken).ToList();
            var failedTokens = eventIngestionResults.Where(result => !result.Success).Select(result => result.LockToken).ToList();
            if (successTokens.Count > 0)
            {
                AcknowledgeResult acknowlegeResult = await eventGridClient.AcknowledgeCloudEventsAsync(topicName, subscription, new AcknowledgeOptions(successTokens));
                _logger.LogInformation("{SuccessTokensCount} Events ingested and acknowledged successfully for {TopicName}.", successTokens.Count, topicName);
                LogLockTokensResult(topicName, acknowlegeResult.SucceededLockTokens, acknowlegeResult.FailedLockTokens);
            }
            if (failedTokens.Count > 0)
            {
                ReleaseResult releaseResult = await eventGridClient.ReleaseCloudEventsAsync(topicName, subscription, new ReleaseOptions(failedTokens));
                _logger.LogInformation("{FailedTokensCount} Events failed ingestion and released successfully for {TopicName}.", failedTokens.Count, topicName);
                LogLockTokensResult(topicName, releaseResult.SucceededLockTokens, releaseResult.FailedLockTokens);
            }
        }

        /// <summary>
        /// Logs the outcome of acknowledging or releasing events based on their lock tokens.
        /// </summary>
        /// <param name="tokenName">The name associated with the lock tokens.</param>
        /// <param name="succeededLockTokens">List of lock tokens that were successfully processed.</param>
        /// <param name="failedLockTokens">List of lock tokens that failed to process.</param>
        private void LogLockTokensResult(string tokenName, IReadOnlyList<string> succeededLockTokens, IReadOnlyList<FailedLockToken> failedLockTokens)
        {
            if (_logger.IsEnabled(LogLevel.Warning) && failedLockTokens.Count > 0)
            {
                _logger.LogWarning("{TokenName} Failed lock token count: {FailedLockTokensCount}", tokenName, failedLockTokens.Count);
                foreach (FailedLockToken failedLockToken in failedLockTokens)
                {
                    _logger.LogWarning("{TokenName} Lock Token: {FailedLockToken}", tokenName, failedLockToken.LockToken);
                    _logger.LogWarning("{TokenName} Error Code: {FailedLockTokenError}", tokenName, failedLockToken.Error);
                    _logger.LogWarning("{TokenName} Error Description: {FailedLockTokenDescription}", tokenName, failedLockToken.ToString());
                }
            }
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("{TokenName} Success lock token count: {SucceededLockTokensCount}", tokenName, succeededLockTokens.Count);
                foreach (string lockToken in succeededLockTokens)
                {
                    _logger.LogDebug("{TokenName} Lock Token: {LockToken}", tokenName, lockToken);
                }
            }
        }

        /// <summary>
        /// Retrieves an existing EventGridClient instance from the cache or creates a new one if it doesn't exist.
        /// </summary>
        /// <param name="topicName">The name of the topic associated with the EventGridClient.</param>
        /// <param name="subscription">The subscription name associated with the EventGridClient.</param>
        /// <param name="topicKey">The access key for the Event Grid topic.</param>
        /// <returns>
        /// An EventGridClient instance configured for the specified topic and subscription.
        /// </returns>
        /// <remarks>
        /// This method ensures that each unique combination of topic name and subscription 
        /// uses the same EventGridClient instance, optimizing resource usage.
        /// </remarks>
        private EventGridClient GetOrCreateEventGridClient(string topicName,  string topicKey)
        {
            return _clients.GetOrAdd(topicName, _ =>
            {
                return CreateEventGridClient(topicKey);
            });
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

        /// <summary>
        /// Represents the result of ingesting an individual event.
        /// </summary>
        /// <param name="Success">Indicates whether the event ingestion was successful.</param>
        /// <param name="LockToken">The lock token associated with the ingested event, used for acknowledging or releasing the event.</param>
        private record EventIngestionResult(bool Success, string LockToken);

    }
}