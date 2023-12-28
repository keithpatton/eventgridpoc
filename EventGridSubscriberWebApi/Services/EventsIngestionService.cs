﻿using Azure;
using Azure.Messaging;
using Azure.Messaging.EventGrid.Namespaces;
using EventGridSubscriberWebApi.Abstractions;
using EventGridSubscriberWebApi.Options;
using Microsoft.Extensions.Options;

namespace EventGridSubscriberWebApi.Services
{
    public class EventsIngestionService(ILoggerFactory loggerFactory, 
        IOptions<EventsIngestionServiceOptions> optionsAccessor, IEventIngestionService eventIngestionService)
        : IEventsIngestionService
    {
        private readonly EventsIngestionServiceOptions _options = optionsAccessor.Value;
        private readonly ILogger _logger = loggerFactory.CreateLogger<EventsIngestionService>();

        /// <summary>
        /// Ingests all events
        /// </summary>
        public async Task IngestAsync()
        {
            var topicConfigs = _options.Topics;
            var tasks = topicConfigs.Select(topic => IngestTopicEventsAsync(topic.Name, topic.Key)).ToList();
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Ingests events for a topic
        /// </summary>
        private async Task IngestTopicEventsAsync(string topicName, string topicKey)
        {
            try
            {
                // NOTE: managed identity support is available
                var eventGridClient = new EventGridClient(new Uri(_options.NamespaceEndpoint), new AzureKeyCredential(topicKey));
                bool eventsToIngest;
                do
                {
                    var maxWaitTime = _options.MaxWaitTime;
                    _logger.LogInformation($"Events requested for {topicName}");
                    ReceiveResult result = await eventGridClient.ReceiveCloudEventsAsync(topicName, _options.Subscription, _options.EventBatchSize, maxWaitTime);

                    eventsToIngest = result.Value.Any();
                    if (eventsToIngest)
                    {
                        _logger.LogInformation($"{result.Value.Count} Events received for {topicName}");
                        var ingestionTasks = result.Value.Select(detail => IngestEventAsync(eventGridClient, topicName, detail)).ToList();
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
        /// Ingests an individual event
        /// </summary>
        /// <remarks>
        /// Note: Ensure idempotency, the same events might be delivered multiple times or in an unexpected order
        /// </remarks>
        private async Task IngestEventAsync(EventGridClient eventgridClient, string topicName, ReceiveDetails detail)
        {
            CloudEvent cloudEvent = detail.Event;
            BrokerProperties brokerProperties = detail.BrokerProperties;
            try
            {
                await eventIngestionService.IngestAsync(cloudEvent);
                AcknowledgeResult acknowlegeResult = await eventgridClient.AcknowledgeCloudEventsAsync(topicName, _options.Subscription, new AcknowledgeOptions(new List<string> { brokerProperties.LockToken }));
                LogLockTokensResult(topicName, acknowlegeResult.SucceededLockTokens, acknowlegeResult.FailedLockTokens);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error ingesting event: {ex.Message}");
                ReleaseResult releaseResult = await eventgridClient.ReleaseCloudEventsAsync(topicName, _options.Subscription, new ReleaseOptions(new List<string> { brokerProperties.LockToken }));
                LogLockTokensResult(topicName, releaseResult.SucceededLockTokens, releaseResult.FailedLockTokens);
            }
        }

        /// <summary>
        /// Log summary for lock tokens
        /// </summary>
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

    }
}
