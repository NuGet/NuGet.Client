using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.LibraryModel;
using NuGet.ProjectModel;

namespace NuGet.Commands
{
    class NoOpRestoreResult : RestoreResult
    {
        public NoOpRestoreResult(bool success, IEnumerable<RestoreTargetGraph> restoreGraphs, IEnumerable<CompatibilityCheckResult> compatibilityCheckResults, IEnumerable<MSBuildOutputFile> msbuildFiles,
            LockFile lockFile, LockFile previousLockFile, string lockFilePath, CacheFile cacheFile, string cacheFilePath, ProjectStyle projectStyle, TimeSpan elapsedTime) :
            base(success, restoreGraphs, compatibilityCheckResults, msbuildFiles, lockFile, previousLockFile, lockFilePath, cacheFile, cacheFilePath, projectStyle, elapsedTime)
        {
        }

        //We override this method because in the case of a no op we don't need to update anything
        public override async Task CommitAsync(ILogger log, CancellationToken token)
        {
        }
        //We override this method because in the case of a no op we don't have any new libraries installed
        public override ISet<LibraryIdentity> GetAllInstalled()
        {
            return new HashSet<LibraryIdentity>();
        }
    }
}
