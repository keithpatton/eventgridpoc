using Azure.Messaging;

namespace EventGridSubscriberWebApi.Abstractions
{
    public interface IRedisLockService
    {
        Task<bool> TryAcquireLockAsync(string lockKey, TimeSpan expiryTime);
        Task ReleaseLockAsync(string lockKey);
    }
}
