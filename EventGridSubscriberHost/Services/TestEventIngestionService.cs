using Azure.Messaging;
using Microsoft.Extensions.Logging;
using Serko.Messaging.EventIngestion.Abstractions;
using Serko.Messaging.EventIngestion.Services;

namespace EventGridSubscriberWebApi.Services
{

    /// <summary>
    /// Ingests an Event using Sql Database
    /// </summary>
    public class TestEventIngestionService : IEventIngestionService
    {
        private readonly ILogger<TestEventIngestionService> _logger;
        private readonly Random _random;

        public TestEventIngestionService(ILogger<TestEventIngestionService> logger)
        {
            _logger = logger;
            _random = new Random();
        }

        public async Task<EventIngestionResultType> IngestAsync(CloudEvent cloudEvent)
        {
            // Generate a random number between 0 and 1
            double randomNumber = _random.NextDouble();

            // Determine the result based on the desired probabilities
            if (randomNumber < 0.95)
            {
                // 95% probability of success
                return EventIngestionResultType.Success;
            }
            else if (randomNumber < 0.99)
            {
                // Additional 4% probability of transient failure
                return EventIngestionResultType.TransientFailure;
            }
            else
            {
                // Remaining 1% probability of rejection
                return EventIngestionResultType.Rejected;
            }
        }
       
    }
}