using Azure.Messaging;

namespace Serko.Messaging.EventPublishing.Model
{
    /// <summary>
    /// Represents an item in the event queue, containing the event data and its associated topic name.
    /// </summary>
    public record EventQueueItem(CloudEvent CloudEvent, string TopicName);
}
