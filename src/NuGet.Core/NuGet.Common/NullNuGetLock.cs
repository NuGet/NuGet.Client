using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Common
{
    /// <summary>
    /// A lock that does nothing.
    /// </summary>
    public class NullNuGetLock : INuGetLock
    {
        /// <summary>
        /// Static instance
        /// </summary>
        public static NullNuGetLock Instance = new NullNuGetLock();

        public Task<T> ExecuteAsync<T>(Func<Task<T>> asyncAction)
        {
            // Execute the action
            return asyncAction();
        }

        public string Id
        {
            get
            {
                return string.Empty;
            }
        }
    }
}
