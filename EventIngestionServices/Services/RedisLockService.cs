using Azure.Identity;
using EventIngestionServices.Abstractions;
using EventIngestionServices.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Wrap;
using StackExchange.Redis;

namespace EventIngestionServices
{

    /// <summary>
    /// Implements a distributed lock mechanism using Redis, with added resiliency features like retries and circuit breaker.
    /// </summary>
    public class RedisLockService : IRedisLockService
    {
        private readonly Lazy<ConnectionMultiplexer> _lazyConnection;
        private IDatabase _database => _lazyConnection.Value.GetDatabase();
        private readonly AsyncPolicyWrap _resiliencePolicy;
        private readonly ILogger<RedisLockService> _logger;
        private readonly RedisLockServiceOptions _options;

        /// <summary>
        /// Initializes a new instance of the RedisLockService class.
        /// </summary>
        /// <param name="optionsAccessor">Configuration options for Redis connection and resiliency settings.</param>
        /// <param name="logger">Logger for logging messages and errors.</param>

        public RedisLockService(IOptions<RedisLockServiceOptions> optionsAccessor, ILogger<RedisLockService> logger)
        {
            _options = optionsAccessor.Value;
            _lazyConnection = new Lazy<ConnectionMultiplexer>(() => InitConnection());
            _logger = logger;
            _resiliencePolicy = InitialiseResiliencyPolicies();
        }

        private ConnectionMultiplexer InitConnection()
        {
            var configurationOptions = ConfigurationOptions.Parse($"{_options.Host}:{_options.Port}");
            configurationOptions.AbortOnConnectFail = false;
            configurationOptions.Ssl = true;
            if (!string.IsNullOrWhiteSpace(_options.PrincipalId))
            {
                configurationOptions.ConfigureForAzureWithTokenCredentialAsync(_options.PrincipalId, new DefaultAzureCredential()).GetAwaiter().GetResult();
            }
            else
            {
                configurationOptions.Password = _options.Password;
            }
            return ConnectionMultiplexer.Connect(configurationOptions);
        }

        /// <summary>
        /// Tries to acquire a distributed lock with the given key and expiry time.
        /// </summary>
        /// <param name="lockKey">The key representing the lock.</param>
        /// <param name="expiryTime">The duration for which the lock should be held.</param>
        /// <returns>True if the lock was successfully acquired, false otherwise.</returns>

        public async Task<bool> TryAcquireLockAsync(string lockKey, TimeSpan expiryTime)
        {
            try
            { 
                return await _resiliencePolicy.ExecuteAsync(async () =>
                {
                    return await _database.StringSetAsync(lockKey, "lock", expiryTime, When.NotExists);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to acquire lock.");
                return false;
            }
        }

        /// <summary>
        /// Releases a previously acquired lock with the given key.
        /// </summary>
        /// <param name="lockKey">The key representing the lock to be released.</param>

        public async Task ReleaseLockAsync(string lockKey)
        {
            try
            {
                await _resiliencePolicy.ExecuteAsync(async () =>
                {
                    await _database.KeyDeleteAsync(lockKey);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to release lock.");
            }
        }


        /// <summary>
        /// Initializes resilience policies including retry and circuit breaker for handling Redis operations.
        /// </summary>
        /// <returns>An AsyncPolicyWrap configured with retry and circuit breaker policies.</returns>

        private AsyncPolicyWrap InitialiseResiliencyPolicies()
        {
            var retryPolicy = Policy
                .Handle<RedisException>()
                .Or<RedisTimeoutException>()
                .WaitAndRetryAsync(
                    retryCount: _options.RetryCount, 
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(_options.RetryDelay.TotalSeconds, retryAttempt)), // Exponential back-off
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(exception, $"Retry Attempt {retryCount}");
                    }
                );

            var circuitBreakerPolicy = Policy
                .Handle<RedisException>()
                .Or<RedisTimeoutException>()
                .AdvancedCircuitBreakerAsync(
                    failureThreshold: _options.CircuitBreakerFailureThreshold,
                    samplingDuration: _options.CircuitBreakerSamplingDuration,
                    minimumThroughput: _options.CircuitBreakerMinimumThroughput,
                    durationOfBreak: _options.CircuitBreakerSamplingDuration,
                    onBreak: (exception, timespan) =>
                    {
                        _logger.LogWarning($"Circuit broken due to {exception.GetType().Name}");
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Circuit reset");
                    },
                    onHalfOpen: () =>
                    {
                        _logger.LogInformation("Circuit is half-open");
                    }
                );

            return circuitBreakerPolicy.WrapAsync(retryPolicy);
        }

    }

}