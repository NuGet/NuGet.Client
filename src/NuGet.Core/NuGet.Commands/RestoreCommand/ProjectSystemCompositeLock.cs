using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Common;

namespace NuGet.Commands
{
    public class ProjectSystemCompositeLock
    {
        // Project path -> lock
        private readonly Dictionary<string, INuGetLock> _locks;

        public ProjectSystemCompositeLock(IEnumerable<INuGetLock> locks)
        {
            if (locks == null)
            {
                throw new ArgumentNullException(nameof(locks));
            }

            _locks = locks
                .Where(e => !string.IsNullOrEmpty(e.Id))
                .GroupBy(e => e.Id, StringComparer.Ordinal)
                .ToDictionary(e => e.Key,
                 e => e.First(),
                 StringComparer.Ordinal);
        }

        /// <summary>
        /// Returns a lock for the project if one exists,
        /// otherwise an empty lock is returned.
        /// </summary>
        public INuGetLock GetProjectLock(string projectPath)
        {
            INuGetLock currentLock = null;

            if (_locks.TryGetValue(projectPath, out currentLock))
            {
                // Return the indexed project lock
                return currentLock;
            }

            // Return a lock that does nothing
            return new NullNuGetLock();
        }
    }
}
