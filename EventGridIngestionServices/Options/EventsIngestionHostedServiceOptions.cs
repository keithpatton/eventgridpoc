namespace EventGridIngestionServices.Options
{
    /// <summary>
    /// Configuration options for EventsIngestionHostedService.
    /// </summary>
    public class EventsIngestionHostedServiceOptions
    {
        /// <summary>
        /// Gets or sets the frequency at which the hosted service polls for events.
        /// </summary>
        /// <remarks>
        /// The default value is set to 30 seconds.
        /// </remarks>
        public TimeSpan PollingFrequency { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the key used for acquiring a Redis lock in the hosted service.
        /// </summary>
        /// <remarks>
        /// The default value is the name of the EventsIngestionHostedService.
        /// </remarks>
        public string RedisLockKey { get; set; } = nameof(EventsIngestionHostedService);

        /// <summary>
        /// Gets or sets the timeout duration for the Redis lock.
        /// </summary>
        /// <remarks>
        /// The default value is set to 5 minutes.
        /// </remarks>
        public TimeSpan RedisLockTimeout { get; set; } = TimeSpan.FromMinutes(5);
    }
}
