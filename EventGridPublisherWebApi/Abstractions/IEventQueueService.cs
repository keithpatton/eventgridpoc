using EventGridPublisherWebApi.Model;

namespace EventGridPublisherWebApi.Abstractions
{
    /// <summary>
    /// Defines the contract for a service that manages event queues for different topics, allowing events to be enqueued and dequeued.
    /// </summary>
    public interface IEventQueueService
    {
        /// <summary>
        /// Enqueues an event into the queue associated with its topic.
        /// </summary>
        /// <param name="eventData">The event data to enqueue, containing the event and its associated topic.</param>
        void EnqueueEvent(EventQueueItem eventQueueItem);

        /// <summary>
        /// Enqueues multiple events into the queue associated with their topics.
        /// </summary>
        /// <param name="events">The collection of event data to enqueue, each containing an event and its associated topic.</param>
        void EnqueueEvents(IEnumerable<EventQueueItem> eventQueueItems);


        /// <summary>
        /// Dequeues a specified number of events from the queue associated with a given topic.
        /// </summary>
        /// <param name="topicName">The name of the topic for which to dequeue events.</param>
        /// <param name="batchSize">The maximum number of events to dequeue.</param>
        /// <returns>A collection of dequeued events.</returns>
        IEnumerable<EventQueueItem> DequeueEvents(string topicName, int batchSize);
    }
}
