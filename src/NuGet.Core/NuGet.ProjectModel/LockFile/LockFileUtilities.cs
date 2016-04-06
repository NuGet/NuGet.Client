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

        // MSBuild for VS2015U1 fails when projects are in the lock file since it treats them as packages.
        // To work around that NuGet will downgrade the lock file if there are only csproj references.
        // Projects with zero project references can go to v2, and projects with xproj references must be
        // at least v2 to work.
        // references should include the parent project
        public static int GetLockFileVersion(IReadOnlyList<ExternalProjectReference> references)
        {
            var version = LockFileFormat.Version;

            // if xproj is used the higher version must be used
            if (references.Any(reference => reference.ExternalProjectReferences.Count > 0)
                && !references.Any(reference =>
                        reference.MSBuildProjectPath?.EndsWith(XProjUtility.XProjExtension) == true))
            {
                // Fallback to v1 for non-xprojs with p2ps
                version = 1;
            }

            return version;
        }
    }
}
