using EventGridSubscriberWebApi.Abstractions;
using EventGridSubscriberWebApi.Options;
using Microsoft.Extensions.Options;

namespace EventGridSubscriberWebApi.Services
{
    public class EventsIngestionHostedService : IHostedService, IDisposable
    {
        private Timer? _timer;
        private Task? _lastExecutionTask;
        private readonly ILogger _logger;
        private readonly IEventsIngestionService _eventsIngestionService;      
        private readonly object _lock = new();
        private readonly IRedisLockService _redisLockService;
        private readonly EventsIngestionHostedServiceOptions _options;

        public EventsIngestionHostedService(IOptions<EventsIngestionHostedServiceOptions> optionsAccessor, 
            ILogger<EventsIngestionHostedService> logger, 
            IEventsIngestionService eventsIngestionService, IRedisLockService redisLockService)
        {
            _options = optionsAccessor.Value;
            _logger = logger;
            _eventsIngestionService = eventsIngestionService;
            _redisLockService = redisLockService;
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            TimeSpan interval = _options.PollingFrequency;
            _timer = new Timer(ExecuteTask, null, TimeSpan.Zero, interval);
            return Task.CompletedTask;
        }

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

        public Task StopAsync(CancellationToken stoppingToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return _lastExecutionTask ?? Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }

}