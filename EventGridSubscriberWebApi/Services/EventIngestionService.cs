using Azure;
using Azure.Data.Tables;
using Azure.Messaging;

namespace EventGridSubscriberWebApi.Services
{

    /// <summary>
    /// Ingests an Event
    /// </summary>
    public class EventIngestionService
    {
        private readonly ILogger _logger;
        private readonly TableClient _tableClient;

        public EventIngestionService(ILoggerFactory loggerFactory, IConfiguration config)
        {
            _logger = loggerFactory.CreateLogger<EventIngestionService>();
            string storageConnectionString = config["StorageConnString"]!;
            string tableName = config["StorageTableName"]!;
            _tableClient = new TableClient(storageConnectionString, tableName);
            InitializeTable();
        }

        /// <summary>
        /// Ingests a Cloud Event (using Table Storage)
        /// </summary>
        public async Task IngestAsync(CloudEvent cloudEvent)
        {
            try
            {
                var formattedTime = cloudEvent.Time?.ToString("yyyyMMddHHmmssfffffff");
                var entity = new CloudEventEntity
                {
                    PartitionKey = cloudEvent.Source,
                    RowKey = $"{formattedTime}-{cloudEvent.Id}",
                    EventData = cloudEvent.Data!.ToString()
                };
                // resiliency is built into the table client
                await _tableClient.AddEntityAsync(entity);
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                // ignore duplicate events
                _logger.LogWarning("Duplicate CloudEvent detected and skipped.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing CloudEvent in Table Storage.");
                throw;
            }
        }

        private async void InitializeTable()
        {
            try
            {
                await _tableClient.CreateIfNotExistsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Table Storage table.");
            }
        }
    }
}
