using Azure;
using Azure.Messaging.EventHubs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EventHubsSubscriberFunc
{
    public class EventsProcessor(ILoggerFactory loggerFactory, IConfiguration config)
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<EventsProcessor>();

        [Function(nameof(CustomisationDomainEventsProcessor))]
        public async Task CustomisationDomainEventsProcessor([EventHubTrigger("customisation", Connection = "NamespaceConnString", ConsumerGroup = "personalisation")] EventData[] events)
        {
            await RunFunctionAsync(config["TopicName1"], events);
        }

        [Function(nameof(LocationDomainEventsProcessor))]
        public async Task LocationDomainEventsProcessor([EventHubTrigger("location", Connection = "NamespaceConnString", ConsumerGroup = "personalisation")] EventData[] events)
        {
            await RunFunctionAsync(config["TopicName2"], events);
        }

        /// <summary>
        /// Core function run for any topic events processor function
        /// </summary>
        private async Task RunFunctionAsync(string topicName, EventData[] events)
        {
            // note: Unlike an Event Grid subscription a consumer group doesn't filter, the filtering must be done at the consumer end
            // This means all domain service events are received and the consumer must filter themselves, not ideal.
            var eventsList = events.Where(e => e.Properties["EventType"].ToString() == config["ConsumerGroup"]).ToList();
            try
            {


                foreach (EventData @event in eventsList)
                {
                    _logger.LogDebug("Event Body: {body}", @event.Body);
                    _logger.LogDebug("Event Type: {eventType}", @event.Properties["EventType"]);

                    // CUSTOM EVENT PROCESSING CODE HERE
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred when running event hubs trigger function for {topicName}");
                throw;
            }

            // Note: The checkpointing on the topic/hub is done automatically as part of successful function completion
            // Batch mode means the events will be resent in the event of failure
            // This is good to the extent that less code is required, but no ability in batch mode to control this
            // for each event without having function process 1 event at a time via host.json configuration
        }

    }
}
