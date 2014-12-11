using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Data
{
    /// <summary>
    /// Session wide URI lock
    /// </summary>
    public class UriLock : IDisposable
    {
        // session wide locks
        private static readonly ConcurrentDictionary<Uri, Guid> _locks = new ConcurrentDictionary<Uri, Guid>();
        private readonly Uri _uri;
        private readonly int _msWait;
        private readonly Guid _guid;

        public UriLock(Uri uri, int msWait=100)
        {
            _uri = uri;
            _msWait = msWait;
            _guid = new Guid();

            GetLock();
        }

        private void GetLock()
        {
            while (!_locks.TryAdd(_uri, _guid))
            {
                // spin lock
                Thread.Sleep(_msWait);
            }
        }

        private void ReleaseLock()
        {
            Guid obj;
            if (!_locks.TryRemove(_uri, out obj))
            {
                Debug.Fail("Missing lock object!");
            }
        }

        public void Dispose()
        {
            ReleaseLock();
        }
    }
}
