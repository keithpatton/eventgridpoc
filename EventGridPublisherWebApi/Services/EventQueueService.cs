using EventGridPublisherWebApi.Abstractions;
using EventGridPublisherWebApi.Model;
using System.Collections.Concurrent;

namespace EventGridPublisherWebApi.Services
{
    public class EventQueueService : IEventQueueService
    {
        private readonly ILogger<EventQueueService> _logger;
        private readonly ConcurrentDictionary<string, ConcurrentQueue<EventQueueItem>> _topicQueues = new();

        public EventQueueService(ILogger<EventQueueService> logger)
        {
            _logger = logger;
        }

        public void EnqueueEvent(EventQueueItem eventQueueItem)
        {
            if (eventQueueItem == null) throw new ArgumentNullException(nameof(eventQueueItem));
            EnqueueEvents(new List<EventQueueItem> { eventQueueItem });
        }

        public void EnqueueEvents(IEnumerable<EventQueueItem> eventQueueItems)
        {
            if (eventQueueItems == null) throw new ArgumentNullException(nameof(eventQueueItems));
            foreach (var eventQueueItem in eventQueueItems)
            {
                _topicQueues.GetOrAdd(eventQueueItem.TopicName, _ => new ConcurrentQueue<EventQueueItem>())
                            .Enqueue(eventQueueItem);
            }
            _logger.LogDebug("{EventCount} event(s) enqueued", eventQueueItems.Count());
        }

        public IEnumerable<EventQueueItem> DequeueEvents(string topicName, int batchSize)
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
            return dequeuedEvents;
        }
    }
}