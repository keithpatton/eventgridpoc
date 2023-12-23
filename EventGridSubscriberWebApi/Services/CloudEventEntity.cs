using Azure;
using Azure.Data.Tables;

namespace EventGridSubscriberWebApi.Services
{
    /// <summary>
    /// Cloud Event Table Entity for Table Storage
    /// </summary>
    public record CloudEventEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        // custom field
        public string EventData { get; set; } = string.Empty;
    }

}
