using EventIngestionServices.Abstractions;
using EventIngestionServices.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventIngestionServices
{
    /// <summary>
    /// A hosted service that periodically ingests events.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="EventsIngestionHostedService"/> class.
    /// </remarks>
    /// <param name="optionsAccessor">Provides configuration options for the service.</param>
    /// <param name="logger">The logger to use for logging information and errors.</param>
    /// <param name="eventsIngestionService">The service responsible for ingesting events.</param>
    /// <param name="redisLockService">The service responsible for managing distributed locks.</param>
    public class EventsIngestionHostedService(IOptions<EventsIngestionHostedServiceOptions> optionsAccessor,
        ILogger<EventsIngestionHostedService> logger,
        IEventsIngestionService eventsIngestionService, IRedisLockService redisLockService) : IHostedService, IDisposable
    {
        private Timer? _timer;
        private Task? _lastExecutionTask;
        private readonly ILogger _logger = logger;
        private readonly IEventsIngestionService _eventsIngestionService = eventsIngestionService;
        private readonly object _lock = new();
        private readonly IRedisLockService _redisLockService = redisLockService;
        private readonly EventsIngestionHostedServiceOptions _options = optionsAccessor.Value;

        /// <summary>
        /// Triggered when the application host is ready to start the service.
        /// </summary>
        /// <param name="stoppingToken">Indicates that the start process should be aborted.</param>
        /// <returns>A task that represents the asynchronous start operation.</returns>
        public Task StartAsync(CancellationToken stoppingToken)
        {
            TimeSpan interval = _options.PollingFrequency;
            _timer = new Timer(ExecuteTask, null, TimeSpan.Zero, interval);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Triggered when the application host is performing a graceful shutdown.
        /// </summary>
        /// <param name="stoppingToken">Indicates that the shutdown process should be aborted.</param>
        /// <returns>A task that represents the asynchronous stop operation.</returns>
        public Task StopAsync(CancellationToken stoppingToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return _lastExecutionTask ?? Task.CompletedTask;
        }

        /// <summary>
        /// Releases all resources used by the <see cref="EventsIngestionHostedService"/>.
        /// </summary>
        public void Dispose()
        {
            _timer?.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Executes the work task. Ensures that only one execution is happening at a time.
        /// </summary>
        /// <param name="state">State object passed by the Timer.</param>
        private void ExecuteTask(object? state)
        {
            lock (_lock)
            {
                if (_lastExecutionTask != null && !_lastExecutionTask.IsCompleted)
                {
                    _logger.LogInformation("Events Ingestion already in progress on this instance, skipping this run");
                    return;
                }
                _lastExecutionTask = DoWorkAsync();
            }
        }

        /// <summary>
        /// Performs the actual work of ingesting events, including acquiring a distributed lock 
        /// before processing and releasing it afterward.
        /// </summary>
        /// <returns>A task representing the asynchronous operation of event ingestion.</returns>
        private async Task DoWorkAsync()
        {
            bool lockAcquired = false;
            try
            {
                lockAcquired = await _redisLockService.TryAcquireLockAsync(_options.RedisLockKey, _options.RedisLockTimeout);
                if (!lockAcquired)
                {
                    _logger.LogInformation("Unable to acquire distributed lock for events ingestion, skipping this run");
                    return;
                }
                await _eventsIngestionService.IngestAsync();
                _logger.LogInformation("Successfully completed events ingestion.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during events ingestion.");
            }
            finally
            {
                if (lockAcquired)
                {
                    await _redisLockService.ReleaseLockAsync(_options.RedisLockKey);
                }
            }
        }
    }
}