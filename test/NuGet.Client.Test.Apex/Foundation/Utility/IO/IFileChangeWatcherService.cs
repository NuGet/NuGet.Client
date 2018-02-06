using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetClient.Test.Foundation.Utility.IO
{
    /// <summary>
    /// Watches a given set of files for content changes. This will watch a file for creation, deletion,
    /// content modification, or renaming. Creation events will fire only a "Changed" event. Other
    /// file timestamp and attribute changes will not be tracked.
    /// </summary>
    public interface IFileChangeWatcherService : IDisposable
    {
        IFileChangeWatcher CreateFileChangeWatcher();
        void StopWatchingAllDirectories();
        void StopWatchingDirectory(string directoryFullPath);
    }
}
