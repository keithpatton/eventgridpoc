using Dapper;
using EventGridPublisherWebApi.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Serko.Messaging.EventPublishing.Abstractions;
using Serko.Messaging.EventPublishing.Model;
using System.Text.Json;

namespace EventGridPublisherWebApi.Services
{
    public class SqlEventQueueService : IEventQueueService
    {
        private readonly ILogger<SqlEventQueueService> _logger;
        private readonly SqlEventQueueServiceOptions _options;
        private readonly Lazy<bool> _databaseInitialised;

        private readonly SemaphoreSlim _eventAvailableSemaphore = new SemaphoreSlim(0);

        public SqlEventQueueService(IOptions<SqlEventQueueServiceOptions> optionsAccessor, ILogger<SqlEventQueueService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = optionsAccessor.Value;
            _databaseInitialised = new Lazy<bool>(InitializeSqlDatabase);
        }

        public async Task EnqueueEventAsync(EventQueueItem eventQueueItem)
        {
            if (eventQueueItem == null) throw new ArgumentNullException(nameof(eventQueueItem));

            await EnqueueEventsAsync(new[] { eventQueueItem });
        }

        public async Task EnqueueEventsAsync(IEnumerable<EventQueueItem> eventQueueItems)
        {
            if (!_databaseInitialised.Value)
            {
                _logger.LogWarning("Database not initialised, unable to enqueue events");
                return;
            };

            if (eventQueueItems == null) throw new ArgumentNullException(nameof(eventQueueItems));
            var items = eventQueueItems.ToList();
            if (!items.Any()) return;

            var sql = @"INSERT INTO EventQueue (EventId, TopicName, EventData)
                VALUES (@EventId, @TopicName, @EventData)";

            using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            var parameters = items.Select(item => new
            {
                EventId = Guid.NewGuid(),
                item.TopicName,
                EventData = JsonSerializer.Serialize(item)
            }).ToList();

            await connection.ExecuteAsync(sql, parameters, transaction);
            transaction.Commit();

            if (eventQueueItems.Any())
            {
                _eventAvailableSemaphore.Release();
            }

            _logger.LogDebug("{EventCount} event(s) enqueued", items.Count);
        }

        public async Task<IEnumerable<EventQueueItem>> DequeueEventsAsync(string topicName, int batchSize)
        {

            if (!_databaseInitialised.Value)
            {
                _logger.LogWarning("Database not initialised, unable to dequeue events");
                return Array.Empty<EventQueueItem>();
            };

            var sql = @"WITH cte AS (
                    SELECT TOP (@BatchSize) *
                    FROM EventQueue
                    WHERE TopicName = @TopicName
                    ORDER BY EventId
                )
                DELETE FROM cte
                OUTPUT DELETED.EventData";

            using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync();

            var eventDatas = await connection.QueryAsync<string>(sql, new { TopicName = topicName, BatchSize = batchSize });

            var dequeuedEvents = eventDatas
                .Select(data => JsonSerializer.Deserialize<EventQueueItem>(data))
                .OfType<EventQueueItem>()
                .ToList();

            _logger.LogDebug("{EventCount} event(s) dequeued for {TopicName}", dequeuedEvents.Count, topicName);
            return dequeuedEvents;
        }

        private bool InitializeSqlDatabase()
        {
            try
            {
                using var connection = new SqlConnection(_options.ConnectionString);
                connection.Open();
                var createTableSql = @"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EventQueue')
                    CREATE TABLE EventQueue (
                        EventId UNIQUEIDENTIFIER PRIMARY KEY,
                        TopicName NVARCHAR(256),
                        EventData NVARCHAR(MAX)
                    )";
                connection.Execute(createTableSql);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing SQL Database.");
                return false;
            }
        }

        public async Task WaitForEventsAsync(CancellationToken cancellationToken)
        {
            await _eventAvailableSemaphore.WaitAsync(cancellationToken);
        }

    }
}
