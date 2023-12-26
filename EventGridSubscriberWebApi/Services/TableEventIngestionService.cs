using Azure;
using Azure.Data.Tables;
using Azure.Messaging;
using EventGridSubscriberWebApi.Abstractions;

namespace EventGridSubscriberWebApi.Services
{

    /// <summary>
    /// Ingests an Event using Tabel Storage
    /// </summary>
    public class TableEventIngestionService : IEventIngestionService
    {
        private readonly ILogger<TableEventIngestionService> _logger;     
        private readonly IConfiguration _config;
        private readonly Lazy<Task<TableClient>> _lazyTableClient;

        public TableEventIngestionService(ILogger<TableEventIngestionService> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
            _lazyTableClient = new Lazy<Task<TableClient>>(InitializeTableClientAsync);
        }

        /// <summary>
        /// Ingests a Cloud Event
        /// </summary>
        public async Task IngestAsync(CloudEvent cloudEvent)
        {
            try
            {
                var formattedTime = cloudEvent.Time?.ToString("yyyyMMddHHmmssfffffff");
                var entity = new CloudEventTableEntity
                {
                    PartitionKey = cloudEvent.Source, // could be topic name
                    RowKey = $"{formattedTime}-{cloudEvent.Id}",
                    EventData = cloudEvent.Data?.ToString() ?? string.Empty // core event data in a custom field
                };
                var tableClient = await _lazyTableClient.Value;
                await tableClient.AddEntityAsync(entity);
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                // ignore duplicate events as these are expected from time to time
                _logger.LogWarning("Duplicate CloudEvent detected and skipped.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing CloudEvent in Table Storage.");
                throw;
            }
        }

        private async Task<TableClient> InitializeTableClientAsync()
        {
            string storageConnectionString = _config["StorageConnString"]!;
            string tableName = _config["StorageTableName"]!;
            var tableClient = new TableClient(storageConnectionString, tableName);
            try
            {
                await tableClient.CreateIfNotExistsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating Table Storage table {tableName}");
                throw;
            }
            return tableClient;
        }

        /// <summary>
        /// Cloud Event Table Entity for Table Storage
        /// </summary>
        private record CloudEventTableEntity : ITableEntity
        {
            public string PartitionKey { get; set; } = string.Empty;
            public string RowKey { get; set; } = string.Empty;
            public DateTimeOffset? Timestamp { get; set; }
            public ETag ETag { get; set; }
            // custom field
            public string EventData { get; set; } = string.Empty;
        }

    }
}