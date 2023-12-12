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
        private EventGridClient _eventGridClient;
        private readonly string _namespaceEndpoint = config["NamespaceEndpoint"];
        private readonly string _topicName = config["TopicName"];
        private readonly string _topicKey = config["EventGridTopicKey"]; 
        private readonly int _eventBatchSize = Convert.ToInt32(config["EventBatchSize"]);
        private readonly string _subscription = config["Subscription"];

        [Function("EventsProcessor")]
        public async Task Run([TimerTrigger("0 */5 * * * *", RunOnStartup = true)] TimerInfo myTimer)
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }

            await ProcessEventsAsync();
        }

        private async Task ProcessEventsAsync()
        {
            // Construct the client using an Endpoint for a namespace as well as the access key
            // NOTE: managed identity support is available in real world
            _eventGridClient = new EventGridClient(new Uri(_namespaceEndpoint), new AzureKeyCredential(_topicKey));

            // Receive the published CloudEvents (includes distributed lock support)
            ReceiveResult result = await _eventGridClient.ReceiveCloudEventsAsync(_topicName, _subscription, _eventBatchSize);

            // Note: possible to receive same event more than once, ensure idempotency
            var processingTasks = new List<Task>();
            foreach (ReceiveDetails detail in result.Value)
            {
                processingTasks.Add(ProcessEventAsync(detail));
            }

            await Task.WhenAll(processingTasks);
        }

        private async Task ProcessEventAsync(ReceiveDetails detail)
        {
            CloudEvent @event = detail.Event;
            BrokerProperties brokerProperties = detail.BrokerProperties;
            try
            {
                _logger.LogDebug(@event.Data.ToString());

                // CUSTOM HANDLING CODE for each event

                AcknowledgeResult acknowlegeResult = await _eventGridClient.AcknowledgeCloudEventsAsync(_topicName, _subscription, new AcknowledgeOptions(new List<string> { brokerProperties.LockToken }));
                LogLockTokensResult(acknowlegeResult.SucceededLockTokens, acknowlegeResult.FailedLockTokens);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event: " + ex.Message);
                ReleaseResult releaseResult = await _eventGridClient.ReleaseCloudEventsAsync(_topicName, _subscription, new ReleaseOptions(new List<string> { brokerProperties.LockToken }));
                LogLockTokensResult(releaseResult.SucceededLockTokens, releaseResult.FailedLockTokens);
            }
        }

        private void LogLockTokensResult(IReadOnlyList<string> succeededLockTokens, IReadOnlyList<FailedLockToken> failedLockTokens)
        {
            // Inspect the Reject result
            _logger.LogInformation($"Failed lock token count: {failedLockTokens.Count}");
            foreach (FailedLockToken failedLockToken in failedLockTokens)
            {
                _logger.LogWarning($"Lock Token: {failedLockToken.LockToken}");
                _logger.LogWarning($"Error Code: {failedLockToken.Error}");
                _logger.LogWarning($"Error Description: {failedLockToken.ToString}");
            }

            _logger.LogInformation($"Success lock token count: {succeededLockTokens.Count}");
            foreach (string lockToken in succeededLockTokens)
            {
                _logger.LogDebug($"Lock Token: {lockToken}");
            }
        }
    }
}
