namespace Serko.Messaging.EventPublishing.Options
{
    /// <summary>
    /// Represents the configuration options for the EventGridPublishingService.
    /// </summary>
    public class EventGridPublishingServiceOptions
    {
        /// <summary>
        /// Gets or sets the namespace endpoint for the Event Grid.
        /// </summary>
        /// <remarks>
        /// This is the endpoint URL for your Event Grid namespace.
        /// </remarks>
        public string NamespaceEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the batch size for event processing.
        /// </summary>
        /// <remarks>
        /// Defines the number of events to publish in each batch. The default is 250.
        /// </remarks>
        public int EventBatchSize { get; set; } = 250;


        /// <summary>
        /// Gets or sets the list of topic configurations for event publishing.
        /// </summary>
        /// <remarks>
        /// Contains configurations for each topic, including name and key.
        /// </remarks>
        public List<TopicConfig> Topics { get; set; } = new List<TopicConfig>();
    }

    /// <summary>
    /// Represents the configuration for a specific Event Grid topic.
    /// </summary>
    public record TopicConfig
    {
        /// <summary>
        /// Gets or sets the name of the Event Grid topic.
        /// </summary>
        /// <remarks>
        /// The unique name of the topic in the Event Grid.
        /// </remarks>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the key (credential) for accessing the Event Grid topic.
        /// </summary>
        /// <remarks>
        /// The authentication key used for accessing the specified topic.
        /// </remarks>
        public string Key { get; set; } = string.Empty;

    }
}
