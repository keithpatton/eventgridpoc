using EventGridSubscriberWebApi.Abstractions;
using EventGridSubscriberWebApi.Options;
using Microsoft.Extensions.Options;
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
        private readonly RedisLockServiceOptions _options;

        public RedisLockService(IOptions<RedisLockServiceOptions> optionsAccessor, ILogger<RedisLockService> logger)
        {
            _options = optionsAccessor.Value;
            _lazyConnection = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(_options.ConnectionString));
            _logger = logger;
            _resiliencePolicy = InitialiseResiliencyPolicies();
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
