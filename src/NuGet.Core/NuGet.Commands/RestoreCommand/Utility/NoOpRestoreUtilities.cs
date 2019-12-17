// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Versioning;

namespace NuGet.Commands
{
    public class NoOpRestoreUtilities
    {
        /// <summary>
        /// The name of the file to use.  When changing this, you should also change <see cref="LockFileFormat.AssetsFileName"/>.
        /// </summary>
        internal const string NoOpCacheFileName = "project.nuget.cache";

        /// <summary>
        /// If the dependencyGraphSpec is not set or <see cref="RestoreRequest.AllowNoOp" /> is <code>false</code>, we cannot no-op on this project restore.
        /// </summary>
        internal static bool IsNoOpSupported(RestoreRequest request)
        {
            return request.DependencyGraphSpec != null && request.AllowNoOp;
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
                return request.Project.RestoreMetadata.CacheFilePath = GetProjectCacheFilePath(cacheRoot);
            }

            return null;
        }

        public static string GetProjectCacheFilePath(string cacheRoot, string projectPath)
        {
            return GetProjectCacheFilePath(cacheRoot);
        }

        public static string GetProjectCacheFilePath(string cacheRoot)
        {
            return Path.Combine(cacheRoot, NoOpCacheFileName);
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

        internal static string GetToolCacheFilePath(string toolDirectory, string toolName)
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
        /// This method verifies that the assets files, props/targets files and all the packages written out in the assets file are present on disk
        /// When the project has opted into packages lock file, it also verified that the lock file is present on disk.
        /// This does not account if the files were manually modified since the last restore
        /// </summary>
        internal static bool VerifyRestoreOutput(RestoreRequest request, CacheFile cacheFile)
        {
            if (!string.IsNullOrWhiteSpace(request.LockFilePath) && !File.Exists(request.LockFilePath))
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
                if (PackagesLockFileUtilities.IsNuGetLockFileEnabled(request.Project))
                {
                    var packageLockFilePath = PackagesLockFileUtilities.GetNuGetLockFilePath(request.Project);
                    if (!File.Exists(packageLockFilePath))
                    {
                        request.Log.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_LockFileNotOnDisk, request.Project.Name, packageLockFilePath));
                        return false;
                    }
                }
                
            }

            if (cacheFile.HasAnyMissingPackageFiles)
            {
                request.Log.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_MissingPackagesOnDisk, request.Project.Name));
                return false;
            }

            return true;
        }

        private static bool IsPackageOnDisk(ISet<PackageIdentity> packagesChecked, IEnumerable<VersionFolderPathResolver> pathResolvers, LocalPackageFileCache packageFileCache, PackageIdentity identity)
        {
            // Each id/version only needs to be checked once
            if (packagesChecked.Add(identity))
            {
                //  Check each package folder. These need to match the order used for restore.
                foreach (var resolver in pathResolvers)
                {
                    // Verify the SHA for each package
                    var hashPath = resolver.GetHashPath(identity.Id, identity.Version);
                    var nupkgMetadataPath = resolver.GetNupkgMetadataPath(identity.Id, identity.Version);

                    if (packageFileCache.Sha512Exists(hashPath) ||
                        packageFileCache.Sha512Exists(nupkgMetadataPath))
                    {
                        return true;
                    }
                }
                return false;
            }
            return true;
        }

        /// <summary>
        /// Generates the dgspec to be used for the no-op optimization
        /// This methods handles the deduping of tools and the ignoring of RestoreSettings
        /// </summary>
        /// <param name="request">The restore request</param>
        /// <returns>The noop happy dg spec</returns>
        /// <remarks> Could be the same instance if no changes were made to the original dgspec</remarks>
        internal static DependencyGraphSpec GetNoOpDgSpec(RestoreRequest request)
        {
            var dgSpec = request.DependencyGraphSpec;

            if (request.Project.RestoreMetadata.ProjectStyle == ProjectStyle.DotnetCliTool || request.Project.RestoreMetadata.ProjectStyle == ProjectStyle.PackageReference)
            {
                var uniqueName = request.DependencyGraphSpec.Restore.First();
                dgSpec = request.DependencyGraphSpec.WithProjectClosure(uniqueName);

                foreach (var projectSpec in dgSpec.Projects)
                {
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
            }

            return dgSpec;
        }

        /// <summary>
        /// Persists the dg file for the given restore request.
        /// This does not do a dirty check!
        /// </summary>
        /// <param name="spec">spec</param>
        /// <param name="dgPath">the dg path</param>
        /// <param name="log">logger</param>
        internal static void PersistDGSpecFile(DependencyGraphSpec spec, string dgPath, ILogger log)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dgPath));
            log.LogVerbose($"Persisting no-op dg to {dgPath}");
            spec.Save(dgPath);
        }

        /// <summary>
        /// Gets the path for dgpsec.json.
        /// The project style that support dgpsec.json persistance are
        /// <see cref="ProjectStyle.PackageReference"/>, <see cref="ProjectStyle.ProjectJson"/>, <see cref="ProjectStyle.Standalone"/>
        /// </summary>
        /// <param name="request"></param>
        /// <returns>The path for the dgspec.json. Null if not appropriate.</returns>
        internal static string GetPersistedDGSpecFilePath(RestoreRequest request)
        {
            if (request.ProjectStyle == ProjectStyle.ProjectJson
                || request.ProjectStyle == ProjectStyle.PackageReference
                || request.ProjectStyle == ProjectStyle.Standalone)
            {
                var outputRoot = request.MSBuildProjectExtensionsPath ?? request.RestoreOutputPath;
                var projFileName = Path.GetFileName(request.Project.RestoreMetadata.ProjectPath);
                var dgFileName = DependencyGraphSpec.GetDGSpecFileName(projFileName);
                return Path.Combine(outputRoot, dgFileName);
            }

            return null;
        }

        /// <summary>
        /// This method will resolve the cache/lock file paths for the tool if available in the cache
        /// This method will set the CacheFilePath and the LockFilePath in the RestoreMetadat if a matching tool is available
        /// </summary>
        internal static void UpdateRequestBestMatchingToolPathsIfAvailable(RestoreRequest request)
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
                    request.Project.RestoreMetadata.CacheFilePath = GetToolCacheFilePath(toolDirectory, ToolRestoreUtility.GetToolIdOrNullFromSpec(request.Project));
                    request.LockFilePath = toolPathResolver.GetLockFilePath(toolDirectory);
                }
            }
        }

        internal static List<string> GetRestoreOutput(RestoreRequest request, LockFile lockFile)
        {
            var pathResolvers = new List<VersionFolderPathResolver>(request.Project.RestoreMetadata.FallbackFolders.Count + 1)
            {
                new VersionFolderPathResolver(request.PackagesDirectory)
            };

            foreach (string restoreMetadataFallbackFolder in request.Project.RestoreMetadata.FallbackFolders)
            {
                pathResolvers.Add(new VersionFolderPathResolver(restoreMetadataFallbackFolder));
            }

            var packageFiles = new List<string>(lockFile.Libraries.Count + request.Project.TargetFrameworks.Sum(i => i.DownloadDependencies.Count));

            foreach (var library in lockFile.Libraries)
            {
                packageFiles.AddRange(GetPackageFiles(request.DependencyProviders.PackageFileCache, library.Name, library.Version, pathResolvers));
            }

            foreach (var targetFrameworkInformation in request.Project.TargetFrameworks)
            {
                foreach (var downloadDependency in targetFrameworkInformation.DownloadDependencies)
                {
                    //TODO: https://github.com/NuGet/Home/issues/7709 - only exact versions are currently supported. The check needs to be updated when version ranges are implemented. 
                    packageFiles.AddRange(GetPackageFiles(request.DependencyProviders.PackageFileCache, downloadDependency.Name, downloadDependency.VersionRange.MinVersion, pathResolvers));
                }
            }

            return packageFiles;
        }

        private static IEnumerable<string> GetPackageFiles(LocalPackageFileCache packageFileCache, string packageId, NuGetVersion version, IEnumerable<VersionFolderPathResolver> resolvers)
        {
            foreach (var resolver in resolvers)
            {
                // Verify the SHA for each package
                var hashPath = resolver.GetHashPath(packageId, version);

                if (packageFileCache.Sha512Exists(hashPath))
                {
                    yield return hashPath;
                    break;
                }

                var nupkgMetadataPath = resolver.GetNupkgMetadataPath(packageId, version);

                if (packageFileCache.Sha512Exists(nupkgMetadataPath))
                {
                    yield return nupkgMetadataPath;
                    break;
                }
            }
        }
    }
}
