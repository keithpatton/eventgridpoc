using Microsoft.Extensions.Logging;
using Serko.Messaging.EventPublishing.Abstractions;
using Serko.Messaging.EventPublishing.Model;
using System.Collections.Concurrent;

namespace Serko.Messaging.EventPublishing.Services
{
    public class MemoryEventQueueService : IEventQueueService
    {
        private readonly ILogger<MemoryEventQueueService> _logger;
        private readonly ConcurrentDictionary<string, ConcurrentQueue<EventQueueItem>> _topicQueues = new();
        private readonly SemaphoreSlim _eventAvailableSemaphore = new SemaphoreSlim(0);

        public MemoryEventQueueService(ILogger<MemoryEventQueueService> logger)
        {
            _logger = logger;
        }

        public Task EnqueueEventAsync(EventQueueItem eventQueueItem)
        {
            if (eventQueueItem == null) throw new ArgumentNullException(nameof(eventQueueItem));
            _topicQueues.GetOrAdd(eventQueueItem.TopicName, _ => new ConcurrentQueue<EventQueueItem>())
                .Enqueue(eventQueueItem);
            _eventAvailableSemaphore.Release();
            _logger.LogDebug("Event enqueued for topic {TopicName}", eventQueueItem.TopicName);
            return Task.CompletedTask;
        }

        public Task EnqueueEventsAsync(IEnumerable<EventQueueItem> eventQueueItems)
        {
            if (eventQueueItems == null) throw new ArgumentNullException(nameof(eventQueueItems));
            foreach (var eventQueueItem in eventQueueItems)
            {
                _topicQueues.GetOrAdd(eventQueueItem.TopicName, _ => new ConcurrentQueue<EventQueueItem>())
                            .Enqueue(eventQueueItem);
            }
            if (eventQueueItems.Any())
            {
                _eventAvailableSemaphore.Release();
            }
            _logger.LogDebug("{EventCount} event(s) enqueued", eventQueueItems.Count());
            return Task.CompletedTask;
        }

        public async Task<IEnumerable<EventQueueItem>> DequeueEventsAsync(string topicName, int batchSize)
        {
            var dequeuedEvents = new List<EventQueueItem>();

            if (_topicQueues.TryGetValue(topicName, out var queue))
            {
                for (int i = 0; i < batchSize && queue.TryDequeue(out var eventData); i++)
                {
                    dequeuedEvents.Add(eventData);
                }
            }

            _logger.LogDebug("{EventCount} event(s) dequeued for {TopicName}", dequeuedEvents.Count, topicName);
            return await Task.FromResult(dequeuedEvents);
        }

        public async Task WaitForEventsAsync(CancellationToken cancellationToken)
        {
            await _eventAvailableSemaphore.WaitAsync(cancellationToken);
        }

    }
}