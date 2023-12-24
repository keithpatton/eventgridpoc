namespace EventGridSubscriberWebApi.Services
{
    public class EventsIngestionHostedService : IHostedService, IDisposable
    {
        private Timer? _timer;
        private readonly IConfiguration _config;
        private readonly ILogger _logger;
        private readonly EventsIngestionService _eventIngestionService;
        private Task? _lastExecutionTask;
        private readonly object _lock = new();

        public EventsIngestionHostedService(IConfiguration config, ILogger<EventsIngestionHostedService> logger, EventsIngestionService eventIngestionService)
        {
            _config = config;
            _logger = logger;
            _eventIngestionService = eventIngestionService;
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            TimeSpan interval = TimeSpan.Parse(_config["PollingFrequency"]!);
            _timer = new Timer(ExecuteTask, null, TimeSpan.Zero, interval);
            return Task.CompletedTask;
        }

        private void ExecuteTask(object? state)
        {
            lock (_lock)
            {
                // check if work already in progress
                if (_lastExecutionTask != null && !_lastExecutionTask.IsCompleted)
                    return;
                _lastExecutionTask = DoWorkAsync();
            }
        }

        private async Task DoWorkAsync()
        {
            try
            {
                await _eventIngestionService.IngestAsync();
                _logger.LogInformation("Successfully completed events ingestion.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during events ingestion.");
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