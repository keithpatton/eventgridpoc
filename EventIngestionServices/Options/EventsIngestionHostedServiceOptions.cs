namespace EventIngestionServices.Options
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
        /// Gets or sets a value indicating whether the hosted service allows concurrent execution of event ingestion tasks.
        /// </summary>
        /// <remarks>
        /// The default value is false.  When set to true, the hosted service will initiate a new event ingestion task 
        /// even if a previous task is still running. 
        /// This allows for overlapping task executions, potentially increasing throughput at the cost of higher resource usage.
        /// The default value is false, meaning the service will wait for an ongoing task to complete before starting a new one, 
        /// ensuring that event ingestion tasks are executed sequentially.
        /// </remarks>
        public bool AllowConcurrentRuns { get; set; } = false;

        /// <summary>
        /// Gets or sets whether usage of Redis lock is disabled
        /// </summary>
        /// <remarks>
        /// The default value is false. Setting to true means hosted service will run on all instances simultaneously.
        /// </remarks>
        public bool RedisLockDisabled { get;set; } = false;

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