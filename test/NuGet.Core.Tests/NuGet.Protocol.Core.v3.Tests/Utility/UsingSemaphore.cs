using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Tests.Utility
{
    public class UsingSemaphore : IDisposable
    {
        private SemaphoreSlim _semaphore;

        private UsingSemaphore(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public static async Task<IDisposable> WaitAsync(SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync();
            return new UsingSemaphore(semaphore);
        }

        public void Dispose()
        {
            _semaphore?.Release();
            _semaphore = null;
        }
    }
}
