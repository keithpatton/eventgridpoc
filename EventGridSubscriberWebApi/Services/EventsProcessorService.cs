using Azure;
using Azure.Messaging;
using Azure.Messaging.EventGrid.Namespaces;

namespace EventGridSubscriberWebApi.Services
{
    public class EventsProcessorService(ILoggerFactory loggerFactory, IConfiguration config, EventProcessorService eventProcessorService)
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<EventsProcessorService>();
        private readonly string _namespaceEndpoint = config["NamespaceEndpoint"]!;
        private readonly int _eventBatchSize = config.GetValue<int>("EventBatchSize");
        private readonly string _subscription = config["Subscription"]!;

        /// <summary>
        /// Processes all events
        /// </summary>
        public async Task ProcessAsync()
        {
            List<Task> tasks =
            [
                // customisation domain events
                ProcessTopicEventsAsync(config["TopicName1"]!, config["TopicKey1"]!),
                // location domain events
                ProcessTopicEventsAsync(config["TopicName2"]!, config["TopicKey2"]!),
            ];
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Core function run for any topic events processor function
        /// </summary>
        private async Task ProcessTopicEventsAsync(string topicName, string topicKey)
        {
            try
            {
                // NOTE: managed identity support is available
                var eventGridClient = new EventGridClient(new Uri(_namespaceEndpoint), new AzureKeyCredential(topicKey));
                await ProcessEventsAsync(eventGridClient, topicName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred when running timer trigger function for {topicName}");
                throw;
            }
        }

        /// <summary>
        /// processes all topic events in batches until there are no more
        /// </summary>
        private async Task ProcessEventsAsync(EventGridClient eventgridClient, string topicName)
        {
            bool eventsToProcess;
            do
            {
                var maxWaitTime = TimeSpan.Parse(config["MaxWaitTime"]!);
                _logger.LogInformation($"Events requested for {topicName}");
                ReceiveResult result = await eventgridClient.ReceiveCloudEventsAsync(topicName, _subscription, _eventBatchSize, maxWaitTime);

                eventsToProcess = result.Value.Any();
                if (eventsToProcess)
                {
                    _logger.LogInformation($"{result.Value.Count} Events received for {topicName}");
                    var processingTasks = result.Value.Select(detail => ProcessEventAsync(eventgridClient, topicName, detail)).ToList();
                    await Task.WhenAll(processingTasks);
                    _logger.LogInformation($"{result.Value.Count} Events processed for {topicName}");
                }
                else
                {
                    _logger.LogInformation($"No Events received for {topicName}");
                }
            } while (eventsToProcess);
        }

        /// <summary>
        /// Processes an individual event
        /// </summary>
        /// <remarks>
        /// Note: Ensure idempotency, the same events might be delivered multiple times or in an unexpected order
        /// </remarks>
        private async Task ProcessEventAsync(EventGridClient eventgridClient, string topicName, ReceiveDetails detail)
        {
            CloudEvent cloudEvent = detail.Event;
            BrokerProperties brokerProperties = detail.BrokerProperties;
            try
            {
                await eventProcessorService.ProcessAsync(cloudEvent);
                AcknowledgeResult acknowlegeResult = await eventgridClient.AcknowledgeCloudEventsAsync(topicName, _subscription, new AcknowledgeOptions(new List<string> { brokerProperties.LockToken }));
                LogLockTokensResult(topicName, acknowlegeResult.SucceededLockTokens, acknowlegeResult.FailedLockTokens);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing event: {ex.Message}");
                ReleaseResult releaseResult = await eventgridClient.ReleaseCloudEventsAsync(topicName, _subscription, new ReleaseOptions(new List<string> { brokerProperties.LockToken }));
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
