// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.ProjectModel;

namespace NuGet.Commands
{
    class CacheFilePathUtilities
    {

        internal static string GetCacheFilePath(RestoreRequest request)
        {
            string cacheFilePath = null;

            if (request.ProjectStyle == ProjectStyle.PackageReference
               || request.ProjectStyle == ProjectStyle.Standalone
               || request.ProjectStyle == ProjectStyle.ProjectJson) // This still goes to the root folder for Project.Json but this will be targetted later
            {
                var projFileName = Path.GetFileName(request.Project.RestoreMetadata.ProjectPath);
                cacheFilePath = request.Project.RestoreMetadata.CacheFilePath = Path.Combine(request.RestoreOutputPath, $"{projFileName}.nuget.cache");
            }

            return cacheFilePath;
        }

        internal static string GetToolCacheFilePath(RestoreRequest request, LockFile lockFile)
        {

            if(request.ProjectStyle != ProjectStyle.DotnetCliTool) { 
                var toolName = ToolRestoreUtility.GetToolIdOrNullFromSpec(request.Project);
                var lockFileLibrary = ToolRestoreUtility.GetToolTargetLibrary(lockFile, toolName);

                if (lockFileLibrary != null)
                {
                    var version = lockFileLibrary.Version;

                    var toolPathResolver = new ToolPathResolver(request.PackagesDirectory);
                    var projFileName = Path.GetFileName(request.Project.RestoreMetadata.ProjectPath); // TODO NK - Do we have this for the dotnet cli tool?
                    return PathUtility.GetDirectoryName(toolPathResolver.GetLockFilePath(
                        toolName,
                        version,
                        lockFile.Targets.First().TargetFramework)) + $"{projFileName}.nuget.cache";
                }
            }
            return null;
        }
    }
}
