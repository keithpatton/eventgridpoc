﻿namespace EventGridSubscriberWebApi.Services
{
    public class EventsProcessorHostedService (IConfiguration config,
        ILogger<EventsProcessorHostedService> logger, EventsProcessorService eventProcessorService)
        : IHostedService, IDisposable
    {
        private Timer? _timer;
        private Task? _lastExecutionTask;
        private readonly object _lock = new();

        public Task StartAsync(CancellationToken stoppingToken)
        {
            TimeSpan interval = TimeSpan.Parse(config["PollingFrequency"]!);
            _timer = new Timer(ExecuteTask, null, TimeSpan.Zero, interval);
            return Task.CompletedTask;
        }

        private void ExecuteTask(object? state)
        {
            lock (_lock)
            {
                if (_lastExecutionTask != null && !_lastExecutionTask.IsCompleted)
                    return;
                _lastExecutionTask = DoWorkAsync();
            }
        }

        private async Task DoWorkAsync()
        {
            try
            {
                await eventProcessorService.ProcessAsync();
                logger.LogInformation("Successfully completed events processing.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred during events processing.");
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