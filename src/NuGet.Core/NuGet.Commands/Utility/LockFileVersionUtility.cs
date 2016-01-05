using System;

namespace NuGet.Commands
{
    public static class LockFileVersionUtility
    {
        private static int? _nuGetLockFileVersion;

        /// <summary>
        /// Determine which lock file version format to use.
        /// </summary>
        public static int GetVersion()
        {
            if (_nuGetLockFileVersion == null)
            {
                // Temporary: Only use the latest version if the environment variable is set
                int lockFileVersion;
                if (Int32.TryParse(Environment.GetEnvironmentVariable("NUGET_LOCKFILE_VERSION"), out lockFileVersion))
                {
                    _nuGetLockFileVersion = lockFileVersion;
                }
                else
                {
                    _nuGetLockFileVersion = 1;
                }
            }

            return (int)_nuGetLockFileVersion;
        }
    }
}
