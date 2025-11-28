using System;
using System.Threading.Tasks;

namespace SMEPilot.FunctionApp.Interfaces
{
    /// <summary>
    /// Feedback2: Interface for distributed locks to prevent race conditions across function instances
    /// </summary>
    public interface IDistributedLock : IDisposable
    {
        /// <summary>
        /// Acquires a distributed lock with the specified key and timeout
        /// </summary>
        /// <param name="key">Lock key (e.g., "enrich-{itemId}")</param>
        /// <param name="timeout">Lock timeout duration</param>
        /// <returns>Lock handle that should be disposed to release the lock</returns>
        Task<IDistributedLock> AcquireAsync(string key, TimeSpan timeout);
        
        /// <summary>
        /// Releases the lock
        /// </summary>
        Task ReleaseAsync();
    }

    /// <summary>
    /// Feedback2: In-memory lock implementation (default, works for single instance)
    /// </summary>
    public class InMemoryDistributedLock : IDistributedLock
    {
        private readonly System.Threading.SemaphoreSlim _semaphore;
        private readonly string _key;
        private bool _disposed = false;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Threading.SemaphoreSlim> _locks = new();

        private InMemoryDistributedLock(string key, System.Threading.SemaphoreSlim semaphore)
        {
            _key = key;
            _semaphore = semaphore;
        }

        public async Task<IDistributedLock> AcquireAsync(string key, TimeSpan timeout)
        {
            var semaphore = _locks.GetOrAdd(key, _ => new System.Threading.SemaphoreSlim(1, 1));
            var acquired = await semaphore.WaitAsync(timeout);
            if (!acquired)
                throw new TimeoutException($"Could not acquire lock for key '{key}' within {timeout.TotalSeconds} seconds");
            return new InMemoryDistributedLock(key, semaphore);
        }

        public Task ReleaseAsync()
        {
            if (!_disposed)
            {
                _semaphore.Release();
                _disposed = true;
            }
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            ReleaseAsync().GetAwaiter().GetResult();
        }
    }
}

