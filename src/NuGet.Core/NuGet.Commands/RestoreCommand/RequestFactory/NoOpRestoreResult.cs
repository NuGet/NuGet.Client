// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.LibraryModel;
using NuGet.ProjectModel;

namespace NuGet.Commands
{
    class NoOpRestoreResult : RestoreResult
    {
        public NoOpRestoreResult(bool success, LockFile lockFile, LockFile previousLockFile, string lockFilePath, CacheFile cacheFile, string cacheFilePath, ProjectStyle projectStyle, TimeSpan elapsedTime) :
            base(success : success, restoreGraphs : null, compatibilityCheckResults : null , 
                msbuildFiles : null, lockFile : lockFile, previousLockFile : previousLockFile, lockFilePath: lockFilePath,
                cacheFile: cacheFile, cacheFilePath: cacheFilePath, projectStyle: projectStyle, elapsedTime: elapsedTime)
        {
        }

        //We override this method because in the case of a no op we don't need to update anything
        public override async Task CommitAsync(ILogger log, CancellationToken token)
        {
            var isTool = ProjectStyle == ProjectStyle.DotnetCliTool;

            if (isTool)
            {
                log.LogDebug($"Tool lock file has not changed. Skipping lock file write. Path: {LockFilePath}");
                log.LogDebug($"No-Op restore. The cache will not be updated."); //TODO - NK Do we need a resource here? 
            }
            else
            {
                log.LogMinimal($"Lock file has not changed. Skipping lock file write. Path: {LockFilePath}");
                log.LogMinimal($"No-Op restore. The cache will not be updated.");
            }
        }

        //We override this method because in the case of a no op we don't have any new libraries installed
        public override ISet<LibraryIdentity> GetAllInstalled()
        {
            return new HashSet<LibraryIdentity>();
        }
    }
}
