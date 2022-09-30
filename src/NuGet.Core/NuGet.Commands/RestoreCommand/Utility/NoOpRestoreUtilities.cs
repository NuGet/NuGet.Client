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
    public class NoOpRestoreUtilities
    {

        /// <summary>
        /// If the dependencyGraphSpec is not set, we cannot no-op on this project restore. 
        /// </summary>
        internal static bool IsNoOpSupported(RestoreRequest request)
        {
            return request.DependencyGraphSpec != null;
        }

        /// <summary>
        /// The cache file path is $(MSBuildProjectExtensionsPath)\$(project).nuget.cache
        /// </summary>
        private static string GetBuildIntegratedProjectCacheFilePath(RestoreRequest request)
        {

            if (request.ProjectStyle == ProjectStyle.ProjectJson
                || request.ProjectStyle == ProjectStyle.PackageReference
                || request.ProjectStyle == ProjectStyle.Standalone)
            {
                var cacheRoot = request.MSBuildProjectExtensionsPath ?? request.RestoreOutputPath;
                return request.Project.RestoreMetadata.CacheFilePath = GetProjectCacheFilePath(cacheRoot, request.Project.RestoreMetadata.ProjectPath);
            }

            return null;
        }

        public static string GetProjectCacheFilePath(string cacheRoot, string projectPath)
        {
            var projFileName = Path.GetFileName(projectPath);
            return Path.Combine(cacheRoot, $"{projFileName}.nuget.cache");
        }

        internal static string GetToolCacheFilePath(RestoreRequest request, LockFile lockFile)
        {
            if (request.ProjectStyle == ProjectStyle.DotnetCliTool && lockFile != null)
            {
                var toolName = ToolRestoreUtility.GetToolIdOrNullFromSpec(request.Project);
                var lockFileLibrary = ToolRestoreUtility.GetToolTargetLibrary(lockFile, toolName);

                if (lockFileLibrary != null)
                {
                    var version = lockFileLibrary.Version;
                    var toolPathResolver = new ToolPathResolver(request.PackagesDirectory);

                    return GetToolCacheFilePath(toolPathResolver.GetToolDirectoryPath(
                        toolName,
                        version,
                        lockFile.Targets.First().TargetFramework), toolName);
                }
            }
            return null;
        }

        public static string GetToolCacheFilePath(string toolDirectory, string toolName)
        {
            return Path.Combine(
                toolDirectory,
                 $"{toolName.ToLowerInvariant()}.nuget.cache");
        }

        /// <summary>
        /// Evaluate the location of the cache file path, based on ProjectStyle.
        /// </summary>
        internal static string GetCacheFilePath(RestoreRequest request)
        {
            return GetCacheFilePath(request, lockFile: null);
        }

        /// <summary>
        /// Evaluate the location of the cache file path, based on ProjectStyle.
        /// </summary>
        internal static string GetCacheFilePath(RestoreRequest request, LockFile lockFile)
        {
            var projectCacheFilePath = request.Project.RestoreMetadata?.CacheFilePath;

            if (string.IsNullOrEmpty(projectCacheFilePath))
            {
                if (request.ProjectStyle == ProjectStyle.PackageReference
                    || request.ProjectStyle == ProjectStyle.Standalone
                    || request.ProjectStyle == ProjectStyle.ProjectJson)
                {
                    projectCacheFilePath = GetBuildIntegratedProjectCacheFilePath(request);
                }
                else if(request.ProjectStyle == ProjectStyle.DotnetCliTool)
                {
                    projectCacheFilePath = GetToolCacheFilePath(request, lockFile);
                }
            }
            return projectCacheFilePath != null ? Path.GetFullPath(projectCacheFilePath) : null;
        }

        /// <summary>
        /// This method verifies that the props/targets files and all the packages written out in the lock file are present on disk
        /// This does not account if the files were manually modified since the last restore
        /// </summary>
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
        /// <summary>
        /// Read out all the packages specified in the existing lock file and verify that they are in the cache
        /// </summary>
        public static bool VerifyPackagesOnDisk(RestoreRequest request)
        {
            var packageFolderPaths = new List<string>();
            packageFolderPaths.Add(request.Project.RestoreMetadata.PackagesPath);
            packageFolderPaths.AddRange(request.Project.RestoreMetadata.FallbackFolders);
            var pathResolvers = packageFolderPaths.Select(path => new VersionFolderPathResolver(path));

            var packagesChecked = new HashSet<PackageIdentity>();

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
                        var nupkgMetadataPath = resolver.GetNupkgMetadataPath(library.Name, library.Version);

                        if (request.DependencyProviders.PackageFileCache.Sha512Exists(hashPath) ||
                            request.DependencyProviders.PackageFileCache.Sha512Exists(nupkgMetadataPath))
                        {
                            found = true;

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

        /// <summary>
        /// Calculates the hash value, used for the no-op optimization, for the request
        /// This methods handles the deduping of tools
        /// Handles the ignoring of RestoreSettings
        /// </summary>
        public static string GetHash(RestoreRequest request)
        {
            if (request.Project.RestoreMetadata.ProjectStyle == ProjectStyle.DotnetCliTool || request.Project.RestoreMetadata.ProjectStyle == ProjectStyle.PackageReference)
            {

                var uniqueName = request.DependencyGraphSpec.Restore.First();
                var dgSpec = request.DependencyGraphSpec.WithProjectClosure(uniqueName);

                foreach (var projectSpec in dgSpec.Projects){
                    // The project path where the tool is declared does not affect restore and is only used for logging and transparency.
                    if (request.Project.RestoreMetadata.ProjectStyle == ProjectStyle.DotnetCliTool)
                    {
                        projectSpec.RestoreMetadata.ProjectPath = null;
                        projectSpec.FilePath = null;
                    }

                    //Ignore the restore settings for package ref projects.
                    //This is set by default for net core projects in VS while it's not set in commandline.
                    //This causes a discrepancy and the project does not cross-client no - op.MSBuild / NuGet.exe vs VS.
                    else if (request.Project.RestoreMetadata.ProjectStyle == ProjectStyle.PackageReference)
                    {
                        projectSpec.RestoreSettings = null;
                    }
                }

                PersistHashedDGFileIfDebugging(dgSpec, request.Log);
                return dgSpec.GetHash();
            }

            PersistHashedDGFileIfDebugging(request.DependencyGraphSpec, request.Log);
            return request.DependencyGraphSpec.GetHash();
        }


        /// <summary>
        /// Write the dg file to a temp location if NUGET_PERSIST_NOOP_DG.
        /// </summary>
        /// <remarks>This is a noop if NUGET_PERSIST_NOOP_DG is not set to true.</remarks>
        private static void PersistHashedDGFileIfDebugging(DependencyGraphSpec spec, ILogger log)
        {
            if (_isPersistDGSet.Value)
            {
                string path;
                var envPath = Environment.GetEnvironmentVariable("NUGET_PERSIST_NOOP_DG_PATH");
                if (!string.IsNullOrEmpty(envPath))
                {
                    path = envPath;
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                }
                else
                {
                    path = Path.Combine(
                        NuGetEnvironment.GetFolderPath(NuGetFolderPath.Temp),
                        "nuget-dg",
                        $"{spec.GetProjectSpec(spec.Restore.FirstOrDefault()).RestoreMetadata.ProjectName}-{DateTime.Now.ToString("yyyyMMddHHmmss")}.dg");
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                }

                log.LogMinimal($"Persisting no-op dg to {path}");

                spec.Save(path);
            }
        }

        private static readonly Lazy<bool> _isPersistDGSet = new Lazy<bool>(() => IsPersistDGSet());

        /// <summary>
        /// True if NUGET_PERSIST_NOOP_DG is set to true.
        /// </summary>
        private static bool IsPersistDGSet()
        {
            var settingValue = Environment.GetEnvironmentVariable("NUGET_PERSIST_NOOP_DG");

            bool val;
            if (!string.IsNullOrEmpty(settingValue)
                && bool.TryParse(settingValue, out val))
            {
                return val;
            }

            return false;
        }

        /// <summary>
        /// This method will resolve the cache/lock file paths for the tool if available in the cache
        /// This method will set the CacheFilePath and the LockFilePath in the RestoreMetadat if a matching tool is available
        /// </summary>
        public static void UpdateRequestBestMatchingToolPathsIfAvailable(RestoreRequest request)
        {
            if (request.ProjectStyle == ProjectStyle.DotnetCliTool)
            {
                // Resolve the lock file path if it exists
                var toolPathResolver = new ToolPathResolver(request.PackagesDirectory);
                var toolDirectory = toolPathResolver.GetBestToolDirectoryPath(
                    ToolRestoreUtility.GetToolIdOrNullFromSpec(request.Project),
                    request.Project.TargetFrameworks.First().Dependencies.First().LibraryRange.VersionRange,
                    request.Project.TargetFrameworks.SingleOrDefault().FrameworkName);

                if (toolDirectory != null) // Only set the paths if a good enough match was found. 
                {
                    request.Project.RestoreMetadata.CacheFilePath = NoOpRestoreUtilities.GetToolCacheFilePath(toolDirectory, ToolRestoreUtility.GetToolIdOrNullFromSpec(request.Project));
                    request.LockFilePath = toolPathResolver.GetLockFilePath(toolDirectory);
                }
            }
        }
    }
}
