namespace EventIngestionServices.Abstractions
{
    /// <summary>
    /// Provides an abstraction for a service that manages distributed locks using Redis.
    /// </summary>
    public interface IRedisLockService
    {
        /// <summary>
        /// Tries to acquire a distributed lock asynchronously.
        /// </summary>
        /// <param name="lockKey">The key used to identify the lock.</param>
        /// <param name="expiryTime">The duration after which the lock should automatically release.</param>
        /// <returns>A task that represents the asynchronous operation. 
        /// The task result contains a boolean indicating whether the lock was successfully acquired.</returns>
        /// <remarks>
        /// This method is used to attempt to acquire a lock identified by the lockKey. If the lock is already held by another process, 
        /// the method should return false. The lock should automatically be released after the expiryTime if not released earlier.
        /// </remarks>
        Task<bool> TryAcquireLockAsync(string lockKey, TimeSpan expiryTime);

        /// <summary>
        /// Releases a previously acquired lock asynchronously.
        /// </summary>
        /// <param name="lockKey">The key used to identify the lock.</param>
        /// <returns>A task that represents the asynchronous operation of releasing the lock.</returns>
        /// <remarks>
        /// This method is used to release a lock identified by the lockKey. It should ensure the lock is released so that other processes 
        /// can acquire it. If the lock does not exist or is not held by the caller, the behavior may vary based on the implementation.
        /// </remarks>
        Task ReleaseLockAsync(string lockKey);
    }
}
