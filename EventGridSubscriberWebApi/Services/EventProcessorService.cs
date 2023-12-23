using Azure.Messaging;

namespace EventGridSubscriberWebApi.Services
{
    /// <summary>
    /// Responsible for processing an individual event
    /// </summary>
    public class EventProcessorService(ILoggerFactory loggerFactory, IConfiguration config)
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<EventProcessorService>();

        /// <summary>
        /// Processes an event
        /// </summary>
        public async Task ProcessAsync(CloudEvent cloudEvent)
        {
            // carry out performant, idempotent processing of the event
            // ensure resiliency added such as Polly retry
            await Task.Delay(10);
        }

    }
}
