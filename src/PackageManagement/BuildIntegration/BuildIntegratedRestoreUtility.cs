// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Helper class for calling the RestoreCommand
    /// </summary>
    public static class BuildIntegratedRestoreUtility
    {
        /// <summary>
        /// Maximum number of threads to use during restore.
        /// </summary>
        public const int MaxRestoreThreads = 8;

        /// <summary>
        /// Restore a build integrated project and update the lock file
        /// </summary>
        public static async Task<RestoreResult> RestoreAsync(
            BuildIntegratedNuGetProject project,
            INuGetProjectContext projectContext,
            IEnumerable<string> sources,
            CancellationToken token)
        {
            // Restore
            var result = await RestoreAsync(project, project.PackageSpec, projectContext, sources, token);

            // Find the lock file path
            var projectJson = new FileInfo(project.JsonConfigPath);
            var lockFilePath = BuildIntegratedProjectUtility.GetLockFilePath(projectJson.FullName);

            // Throw before writing if this has been canceled
            token.ThrowIfCancellationRequested();

            // Write out the lock file
            var lockFileFormat = new LockFileFormat();
            lockFileFormat.Write(lockFilePath, result.LockFile);

            return result;
        }

        /// <summary>
        /// Restore without writing the lock file
        /// </summary>
        internal static async Task<RestoreResult> RestoreAsync(
            BuildIntegratedNuGetProject project,
            PackageSpec packageSpec,
            INuGetProjectContext projectContext,
            IEnumerable<string> sources,
            CancellationToken token)
        {
            // Restoring packages
            projectContext.Log(MessageLevel.Info, Strings.BuildIntegratedPackageRestoreStarted, project.ProjectName);

            var packageSources = sources.Select(source => new PackageSource(source));
            var request = new RestoreRequest(packageSpec, packageSources, BuildIntegratedProjectUtility.GetGlobalPackagesFolder());
            request.MaxDegreeOfConcurrency = MaxRestoreThreads;

            // Find the full closure of project.json files and referenced projects
            var projectReferences = await project.GetProjectReferenceClosureAsync();
            request.ExternalProjects = projectReferences.Select(reference => BuildIntegratedProjectUtility.ConvertProjectReference(reference)).ToList();

            token.ThrowIfCancellationRequested();

            var command = new RestoreCommand(new ProjectContextLogger(projectContext));

            // Execute the restore
            var result = await command.ExecuteAsync(request);

            // Report a final message with the Success result
            if (result.Success)
            {
                projectContext.Log(MessageLevel.Info, Strings.BuildIntegratedPackageRestoreSucceeded, project.ProjectName);
            }
            else
            {
                projectContext.Log(MessageLevel.Info, Strings.BuildIntegratedPackageRestoreFailed, project.ProjectName);
            }

            return result;
        }
    }
}
