using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NuGet.ProjectModel
{
    public static class LockFileUtilities
    {
        /// <summary>
        /// Returns the lockfile if it exists, otherwise null.
        /// </summary>
        public static LockFile GetLockFile(string lockFilePath, Common.ILogger logger)
        {
            LockFile lockFile = null;

            if (File.Exists(lockFilePath))
            {
                var format = new LockFileFormat();

                // A corrupt lock file will log errors and return null
                lockFile = format.Read(lockFilePath, logger);
            }

            return lockFile;
        }
    }
}
