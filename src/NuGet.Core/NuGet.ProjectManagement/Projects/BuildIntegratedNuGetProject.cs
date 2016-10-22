// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Frameworks;
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
    public abstract class BuildIntegratedNuGetProject : NuGetProject, INuGetIntegratedProject, IDependencyGraphProject
    {
        protected BuildIntegratedNuGetProject() { }

        /// <summary>
        /// Project name
        /// </summary>
        public abstract string ProjectName { get; }

        public abstract string MSBuildProjectPath { get; }

        /// <summary>
        /// Returns the path to the assets file or the lock file.
        /// </summary>
        public abstract string AssetsFilePath { get; }

        public abstract Task<IReadOnlyList<PackageSpec>> GetPackageSpecsAsync(DependencyGraphCacheContext context);

        /// <summary>
        /// Script executor hook
        /// </summary>
        public abstract Task<bool> ExecuteInitScriptAsync(
            PackageIdentity identity,
            string packageInstallPath,
            INuGetProjectContext projectContext,
            bool throwOnFailure);

        public virtual async Task<bool> IsRestoreRequired(
                    IEnumerable<VersionFolderPathResolver> pathResolvers,
                    ISet<PackageIdentity> packagesChecked,
                    DependencyGraphCacheContext context)
        {
            var lockFilePath = AssetsFilePath;

            if (!File.Exists(lockFilePath))
            {
                // If the lock file does not exist a restore is needed
                return true;
            }

            var lockFileFormat = new LockFileFormat();
            LockFile lockFile;
            try
            {
                lockFile = lockFileFormat.Read(lockFilePath, context.Logger);
            }
            catch
            {
                // If the lock file is invalid, then restore.
                return true;
            }

            // Ignore tools here
            var specs = await GetPackageSpecsAsync(context);

            var packageSpec = specs.FirstOrDefault(e => e.RestoreMetadata.OutputType != RestoreOutputType.Standalone
                && e.RestoreMetadata.OutputType != RestoreOutputType.DotnetCliTool);

            if (!lockFile.IsValidForPackageSpec(packageSpec, LockFileFormat.Version))
            {
                // The project.json file has been changed and the lock file needs to be updated.
                return true;
            }

            // Verify all libraries are on disk
            var packages = lockFile.Libraries.Where(library => library.Type == LibraryType.Package);

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
                                return true;
                            }

                            // Skip checking the rest of the package folders
                            break;
                        }
                    }

                    if (!found)
                    {
                        // A package is missing
                        return true;
                    }
                }
            }

            return false;
        }
    }
}