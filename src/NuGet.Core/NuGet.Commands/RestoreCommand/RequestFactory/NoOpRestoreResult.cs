// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.LibraryModel;
using NuGet.ProjectModel;

namespace NuGet.Commands
{
    public class NoOpRestoreResult : RestoreResult
    {
        public NoOpRestoreResult(bool success, LockFile lockFile, LockFile previousLockFile, string lockFilePath, CacheFile cacheFile, string cacheFilePath, ProjectStyle projectStyle, TimeSpan elapsedTime) :
            base(success : success, restoreGraphs : null, compatibilityCheckResults : new List<CompatibilityCheckResult>() , 
                msbuildFiles : null, lockFile : lockFile, previousLockFile : previousLockFile, lockFilePath: lockFilePath,
                cacheFile: cacheFile, cacheFilePath: cacheFilePath, projectStyle: projectStyle, elapsedTime: elapsedTime)
        {
        }

        //We override this method because in the case of a no op we don't need to update anything
#pragma warning disable CS1998 // This async method lacks 'await' operators and will run synchronously.
        public override async Task CommitAsync(ILogger log, CancellationToken token)
#pragma warning restore CS1998 // This async method lacks 'await' operators and will run synchronously.
        {
            var isTool = ProjectStyle == ProjectStyle.DotnetCliTool;

            if (isTool)
            {
                log.LogInformation(string.Format(CultureInfo.CurrentCulture,
                        Strings.Log_ToolSkippingAssetsFile,
                        LockFilePath));
                log.LogVerbose(string.Format(CultureInfo.CurrentCulture,
                        Strings.Log_SkippingCacheFile,
                        CacheFilePath));
            }
            else
            {
                log.LogInformation(string.Format(CultureInfo.CurrentCulture,
                                        Strings.Log_SkippingAssetsFile,
                                        LockFilePath));
                log.LogVerbose(string.Format(CultureInfo.CurrentCulture,
                        Strings.Log_SkippingCacheFile,
                        CacheFilePath));
            }
        }

        //We override this method because in the case of a no op we don't have any new libraries installed
        public override ISet<LibraryIdentity> GetAllInstalled()
        {
            return new HashSet<LibraryIdentity>();
        }
    }
}
