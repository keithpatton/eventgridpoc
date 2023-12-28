namespace EventGridSubscriberWebApi.Options
{
    /// <summary>
    /// Represents the configuration options for the EventsIngestionService.
    /// </summary>
    public class EventsIngestionServiceOptions
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
        /// Defines the number of events to fetch in each batch. The default is 100.
        /// </remarks>
        public int EventBatchSize { get; set; } = 100;

        /// <summary>
        /// Gets or sets the subscription name for the Event Grid.
        /// </summary>
        /// <remarks>
        /// Specifies the name of the subscription for event consumption.
        /// </remarks>
        public string Subscription { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the maximum wait time for events to be available.
        /// </summary>
        /// <remarks>
        /// Determines how long the service should wait for events. The default is 10 seconds.
        /// </remarks>
        public TimeSpan MaxWaitTime { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Gets or sets the list of topic configurations for event ingestion.
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
