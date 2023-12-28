namespace EventGridSubscriberWebApi.Options
{
    /// <summary>
    /// Configuration options for RedisLockService.
    /// </summary>
    public class RedisLockServiceOptions
    {
        /// <summary>
        /// Gets or sets the Redis connection string.
        /// </summary>
        /// <remarks>
        /// The connection string is required to connect to the Redis server.
        /// </remarks>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of retry attempts for Redis operations.
        /// </summary>
        /// <remarks>
        /// The default value is set to 3 retries.
        /// </remarks>
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// Gets or sets the initial delay between retry attempts.
        /// </summary>
        /// <remarks>
        /// The default value is set to 2 seconds.
        /// </remarks>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Gets or sets the failure rate threshold for opening the circuit breaker.
        /// </summary>
        /// <remarks>
        /// The default value is set to a 10% failure rate.
        /// </remarks>
        public double CircuitBreakerFailureThreshold { get; set; } = 0.1;

        /// <summary>
        /// Gets or sets the duration for sampling the failure rate.
        /// </summary>
        /// <remarks>
        /// The default value is set to 15 minutes.
        /// </remarks>
        public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Gets or sets the duration for which the circuit breaker remains open.
        /// </summary>
        /// <remarks>
        /// The default value is set to 5 minutes.
        /// </remarks>
        public TimeSpan CircuitBreakerDurationOfBreak { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the minimum number of operations over the sampling period before calculating the failure rate.
        /// </summary>
        /// <remarks>
        /// The default value is set to 100 operations.
        /// </remarks>
        public int CircuitBreakerMinimumThroughput { get; set; } = 100;
    }
}
