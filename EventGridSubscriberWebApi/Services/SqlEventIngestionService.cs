using Azure.Messaging;
using Dapper;
using Serko.Messaging.EventIngestion.Abstractions;
using EventGridSubscriberWebApi.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace EventGridSubscriberWebApi.Services
{

    /// <summary>
    /// Ingests an Event using Sql Database
    /// </summary>
    public class SqlEventIngestionService : IEventIngestionService
    {
        private readonly ILogger<SqlEventIngestionService> _logger;
        private readonly SqlEventIngestionServiceOptions _options;
        private readonly Lazy<bool> _databaseInitialised;

        public SqlEventIngestionService(IOptions<SqlEventIngestionServiceOptions> optionsAccessor, ILogger<SqlEventIngestionService> logger)
        {
            _logger = logger;
            _options = optionsAccessor.Value;
            _databaseInitialised = new Lazy<bool>(InitializeSqlDatabase);
        }

        public async Task IngestAsync(CloudEvent cloudEvent)
        {
            try
            {
                if (!_databaseInitialised.Value)
                {
                    _logger.LogWarning("Database not initialised, unable to ingest event");
                    return;
                };

                var sqlCheck = "SELECT COUNT(1) FROM Events WHERE EventId = @EventId";
                using var connection = new SqlConnection(_options.ConnectionString);
                connection.Open();

                var exists = await connection.ExecuteScalarAsync<bool>(sqlCheck, new { EventId = cloudEvent.Id });

                if (exists)
                {
                    _logger.LogWarning("Duplicate CloudEvent with ID {CloudEventId} detected and skipped.", cloudEvent.Id);
                    return;
                }

                var sqlInsert = @"INSERT INTO Events (EventId, EventTime, EventSource, EventType, EventData)
                        VALUES (@EventId, @EventTime, @EventSource, @EventType, @EventData)";

                if (!Guid.TryParse(cloudEvent.Id, out var eventId))
                {
                    throw new InvalidOperationException($"CloudEvent ID {cloudEvent.Id} is not a valid Guid");
                }

                var parameters = new
                {
                    EventId = eventId,
                    EventTime = cloudEvent.Time,
                    EventSource = cloudEvent.Source,
                    EventType = cloudEvent.Type,
                    EventData = cloudEvent.Data?.ToString() ?? string.Empty
                };
                await connection.ExecuteAsync(sqlInsert, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing CloudEvent.");
                throw;
            }
        }

        private bool InitializeSqlDatabase()
        {
            var connectionString = _options.ConnectionString;
            var connection = new SqlConnection(connectionString);

            try
            {
                connection.Open();
                var createTableSql = @"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Events' and xtype='U')
                    CREATE TABLE Events (
                        EventId UNIQUEIDENTIFIER PRIMARY KEY,
                        EventTime DATETIME2,
                        EventSource NVARCHAR(256),
                        EventType NVARCHAR(256),
                        EventData NVARCHAR(MAX)
                    )";
                connection.Execute(createTableSql);
                 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing SQL Database.");
                throw;
            }
            return true;
        }
    }
}