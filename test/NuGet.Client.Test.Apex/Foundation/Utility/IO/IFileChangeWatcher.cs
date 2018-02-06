using System;
using System.Collections.Generic;
using System.IO;
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
    public interface IFileChangeWatcher : IDisposable
    {
        /// <summary>
        /// Fired when a file is changed
        /// </summary>
        /// <remarks>
        /// When a rename occurs, the ORIGINAL filename is tracked, NOT the new filename. If you want to track the
        /// new name, follow up with a StopWatchingFile/WatchFile.
        /// </remarks>
        event EventHandler<FileSystemEventArgs> FileChanged;

        void WatchFile(string filePath);
        void StopWatchingFile(string filePath);
    }
}
