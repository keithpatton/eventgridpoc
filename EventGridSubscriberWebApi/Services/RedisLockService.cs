using EventGridSubscriberWebApi.Abstractions;
using Polly;
using Polly.Wrap;
using StackExchange.Redis;

namespace EventGridSubscriberWebApi.Services
{

    /// <summary>
    /// Provides a resilient lock using Redis
    /// </summary>
    public class RedisLockService : IRedisLockService
    {
        private readonly Lazy<ConnectionMultiplexer> _lazyConnection;
        private IDatabase _database => _lazyConnection.Value.GetDatabase();
        private readonly AsyncPolicyWrap _resiliencePolicy;
        private readonly ILogger<RedisLockService> _logger;

        public RedisLockService(Lazy<ConnectionMultiplexer> lazyConnection, ILogger<RedisLockService> logger)
        {
            _lazyConnection = lazyConnection;
            _logger = logger;

            // retry for Redis specific exceptions only coming from the Redis Cache Layer
            var retryPolicy = Policy
                .Handle<RedisException>()
                .Or<RedisTimeoutException>()
                .WaitAndRetryAsync(
                    3, // Number of retries
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential back-off
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(exception, $"Retry Attempt {retryCount}");
                    }
                );

            var circuitBreakerPolicy = Policy
                .Handle<RedisException>()
                .Or<RedisTimeoutException>()
                .AdvancedCircuitBreakerAsync(
                    failureThreshold: 0.1, // 10% failure rate
                    samplingDuration: TimeSpan.FromMinutes(15), // Over a 15-minute period
                    minimumThroughput: 100, // Minimum number of actions within the sampling period
                    durationOfBreak: TimeSpan.FromMinutes(5), // Circuit stays open for 5 minutes
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

            _resiliencePolicy = circuitBreakerPolicy.WrapAsync(retryPolicy);
        }

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

    }

}
