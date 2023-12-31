using Azure;
using Azure.Messaging;
using Azure.Messaging.EventGrid.Namespaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EventGridSubscriberFunc
{
    public class EventsProcessor(ILoggerFactory loggerFactory, IConfiguration config)
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<EventsProcessor>();
        private readonly string _namespaceEndpoint = config["NamespaceEndpoint"];
        private readonly int _eventBatchSize = Convert.ToInt32(config["EventBatchSize"]);
        private readonly string _subscription = config["Subscription"];

        [Function(nameof(CustomisationDomainEventsProcessor))]
        public async Task CustomisationDomainEventsProcessor([TimerTrigger("%PollingFrequency%", RunOnStartup = true)] TimerInfo myTimer)
        {
            await RunFunctionAsync(myTimer, config["TopicName1"], config["TopicKey1"]);
        }

        [Function(nameof(LocationDomainEventsProcessor))]
        public async Task LocationDomainEventsProcessor([TimerTrigger("%PollingFrequency%", RunOnStartup = true)] TimerInfo myTimer)
        {
            await RunFunctionAsync(myTimer, config["TopicName2"], config["TopicKey2"]);
        }

        /// <summary>
        /// Core function run for any topic events processor function
        /// </summary>
        private async Task RunFunctionAsync(TimerInfo myTimer, string topicName, string topicKey)
        {
            try
            {
                // NOTE: managed identity support is available in real world
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
                // Receive any published CloudEvents (includes distributed lock support)
                var maxWaitTime = TimeSpan.Parse(config["MaxWaitTime"]);
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
                _logger.LogDebug(cloudEvent!.Data!.ToString());

                // CUSTOM HANDLING CODE for each event 
                // Ensure idempoten, performant and resilient (polly retry?) to failure. 

                AcknowledgeResult acknowlegeResult = await eventgridClient.AcknowledgeCloudEventsAsync(topicName, _subscription, new AcknowledgeOptions(new List<string> { brokerProperties.LockToken }));
                LogLockTokensResult(topicName, acknowlegeResult.SucceededLockTokens, acknowlegeResult.FailedLockTokens);

            }
            catch (Exception ex)
            {

                // Note that rejection will ensure the event is not re-processed.
                // This will NOT trigger dead-lettering at the subscription level (which is around deliverability from the event grid subscription)
                // Understanding this, this operation is essentially a logged circuit breaker by default. 
                _logger.LogError(ex, "Error processing event: " + ex.Message);
                RejectResult rejectResult = await eventgridClient.RejectCloudEventsAsync(topicName, _subscription, new RejectOptions(new List<string> { brokerProperties.LockToken }));
                LogLockTokensResult(topicName, rejectResult.SucceededLockTokens, rejectResult.FailedLockTokens);
            }
        }

        /// <summary>
        /// Log summary for lock tokens
        /// </summary>
        private void LogLockTokensResult(string tokenName, IReadOnlyList<string> succeededLockTokens, IReadOnlyList<FailedLockToken> failedLockTokens)
        {
            // Inspect the Reject result
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
