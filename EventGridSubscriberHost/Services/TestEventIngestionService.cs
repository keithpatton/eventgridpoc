using Azure.Messaging;
using Microsoft.Extensions.Logging;
using Serko.Messaging.EventIngestion.Abstractions;

namespace EventGridSubscriberWebApi.Services
{

    /// <summary>
    /// Ingests an Event using Sql Database
    /// </summary>
    public class TestEventIngestionService : IEventIngestionService
    {
        private readonly ILogger<TestEventIngestionService> _logger;

        public TestEventIngestionService(ILogger<TestEventIngestionService> logger)
        {
            _logger = logger;
        }

        public async Task IngestAsync(CloudEvent cloudEvent)
        {
            _logger.LogInformation($"Cloud Event {cloudEvent.Id} received");
            await Task.CompletedTask;
        }

       
    }
}