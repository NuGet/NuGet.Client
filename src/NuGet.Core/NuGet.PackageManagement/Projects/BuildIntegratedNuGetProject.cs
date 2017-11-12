// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.ProjectManagement.Projects
{
    /// <summary>
    /// A NuGet integrated MSBuild project.k
    /// These projects contain a project.json or package references in CSProj
    /// </summary>
    [DebuggerDisplay("{ProjectName} [{ProjectStyle}]")]
    public abstract class BuildIntegratedNuGetProject 
        : NuGetProject
        , INuGetIntegratedProject
        , IDependencyGraphProject
    {
        protected BuildIntegratedNuGetProject()
        {
            _dependencyVersionLookup = new DependencyVersionLookup();
        }

        protected DependencyVersionLookup _dependencyVersionLookup;
        public DependencyVersionLookup Lookup => _dependencyVersionLookup;

        /// <summary>
        /// Gets timestamp of assets file if exists, if not return null
        /// </summary>
        public async Task<DateTime?> GetAssetsFileTimestampIFExistsAsync()
        {
            var lockFilePath = await GetAssetsFilePathOrNullAsync();

            if (lockFilePath == null || !File.Exists(lockFilePath))
            {
                return null;
            }

            var lockFileInfo = new FileInfo(lockFilePath);
            return lockFileInfo.CreationTime;
        }

        public Task<IReadOnlyList<PackageIdentity>> GetTopLevelDependencies()
        {
            return IntegratedProjectUtility.GetProjectPackageDependenciesAsync(this, false);
        }

        /// <summary>
        /// Project name
        /// </summary>
        public abstract string ProjectName { get; }

        public abstract string MSBuildProjectPath { get; }

        /// <summary>
        /// Returns the path to the assets file or the lock file. Throws an exception if the assets file path cannot be
        /// determined.
        /// </summary>
        public abstract Task<string> GetAssetsFilePathAsync();

        public abstract Task<string> GetCacheFilePathAsync();

        /// <summary>
        /// Returns the path to the assets file or the lock file. Returns null if the assets file path cannot be
        /// determined.
        /// </summary>
        public abstract Task<string> GetAssetsFilePathOrNullAsync();

        public abstract Task<IReadOnlyList<PackageSpec>> GetPackageSpecsAsync(DependencyGraphCacheContext context);

        public abstract Task<bool> InstallPackageAsync(
            string packageId,
            VersionRange range,
            INuGetProjectContext nuGetProjectContext,
            BuildIntegratedInstallationContext installationContext,
            CancellationToken token);

        public override sealed Task<bool> InstallPackageAsync(
            PackageIdentity packageIdentity,
            DownloadResourceResult downloadResourceResult,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token)
        {
            throw new NotImplementedException("This API should not be called for BuildIntegratedNuGetProject.");
        }
    }
}