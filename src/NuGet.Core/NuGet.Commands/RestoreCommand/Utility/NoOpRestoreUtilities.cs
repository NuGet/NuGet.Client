// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;

namespace NuGet.Commands
{
    internal class NoOpRestoreUtilities
    {

        internal static bool IsNoOpSupported(RestoreRequest request)
        {
            return request.DependencyGraphSpec != null && request.ProjectStyle != ProjectStyle.DotnetCliTool;
        }

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

            if (request.ProjectStyle != ProjectStyle.DotnetCliTool)
            {
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
        /// <summary>
        /// Evaluate the location of the cache file path, based on ProjectStyle.
        /// The lockFile is used to evaluate the cache path for tools
        /// </summary>
        internal static string GetCacheFilePath(LockFile lockFile, RestoreRequest request)
        {
            var projectCacheFilePath = request.Project.RestoreMetadata?.CacheFilePath;

            if (string.IsNullOrEmpty(projectCacheFilePath))
            {
                if (request.ProjectStyle == ProjectStyle.PackageReference
                    || request.ProjectStyle == ProjectStyle.Standalone)
                {
                    projectCacheFilePath = GetCacheFilePath(request);
                }
                else if (request.ProjectStyle == ProjectStyle.ProjectJson)
                {
                    projectCacheFilePath = GetCacheFilePath(request);
                }
                else if (request.ProjectStyle == ProjectStyle.DotnetCliTool)
                {

                    projectCacheFilePath = GetToolCacheFilePath(request, lockFile);
                }

            }
            return projectCacheFilePath != null ? Path.GetFullPath(projectCacheFilePath) : null;
        }


        public static bool VerifyAssetsAndMSBuildFilesAndPackagesArePresent(RestoreRequest request)
        {

            if (!File.Exists(request.ExistingLockFile?.Path))
            {
                request.Log.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_AssetsFileNotOnDisk, request.Project.Name));
                return false;
            }

            if (request.ProjectStyle == ProjectStyle.PackageReference || request.ProjectStyle == ProjectStyle.Standalone)
            {
                var targetsFilePath = BuildAssetsUtils.GetMSBuildFilePath(request.Project, request, "targets");
                if (!File.Exists(targetsFilePath))
                {
                    request.Log.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_TargetsFileNotOnDisk, request.Project.Name, targetsFilePath));
                    return false;
                }
                var propsFilePath = BuildAssetsUtils.GetMSBuildFilePath(request.Project, request, "props");
                if (!File.Exists(propsFilePath))
                {
                    request.Log.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_PropsFileNotOnDisk, request.Project.Name, propsFilePath));
                    return false;
                }
            }

            if (!VerifyPackagesOnDisk(request))
            {
                request.Log.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_MissingPackagesOnDisk, request.Project.Name));
                return false;
            }
            return true;
        }

        public static bool VerifyPackagesOnDisk(RestoreRequest request)
        {
            var packageFolderPaths = new List<string>();
            packageFolderPaths.Add(request.Project.RestoreMetadata.PackagesPath);
            packageFolderPaths.AddRange(request.Project.RestoreMetadata.FallbackFolders);
            var pathResolvers = packageFolderPaths.Select(path => new VersionFolderPathResolver(path));

            ISet<PackageIdentity> packagesChecked = new HashSet<PackageIdentity>();

            var packages = request.ExistingLockFile.Libraries.Where(library => library.Type == LibraryType.Package);

            foreach (var library in packages)
            {
                var identity = new PackageIdentity(library.Name, library.Version);

                // Each id/version only needs to be checked once
                if (packagesChecked.Add(identity))
                {
                    var found = false;

                    //  Check each package folder. These need to match the order used for restore.
                    foreach (var resolver in pathResolvers)
                    {
                        // Verify the SHA for each package
                        var hashPath = resolver.GetHashPath(library.Name, library.Version);

                        if (File.Exists(hashPath))
                        {
                            found = true;
                            var sha512 = File.ReadAllText(hashPath);

                            if (library.Sha512 != sha512)
                            {
                                // A package has changed
                                return false;
                            }

                            // Skip checking the rest of the package folders
                            break;
                        }
                    }

                    if (!found)
                    {
                        // A package is missing
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
