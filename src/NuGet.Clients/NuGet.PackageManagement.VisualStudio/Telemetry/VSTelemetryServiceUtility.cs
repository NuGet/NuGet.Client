// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using NuGet.Common;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.Telemetry
{
    /// <summary>
    /// Some utility apis for telemetry operations.
    /// </summary>
    public static class VSTelemetryServiceUtility
    {
        public static readonly Lazy<string> NuGetVersion
            = new Lazy<string>(() => ClientVersionUtility.GetNuGetAssemblyVersion());

        /// <summary>
        /// Create ActionTelemetryEvent instance.
        /// </summary>
        /// <param name="projects"></param>
        /// <param name="operationType"></param>
        /// <param name="source"></param>
        /// <param name="startTime"></param>
        /// <param name="status"></param>
        /// <param name="statusMessage"></param>
        /// <param name="packageCount"></param>
        /// <param name="endTime"></param>
        /// <param name="duration"></param>
        /// <returns></returns>
        public static VSActionsTelemetryEvent GetActionTelemetryEvent(
            string operationId,
            IEnumerable<NuGetProject> projects,
            NuGetOperationType operationType,
            OperationSource source,
            DateTimeOffset startTime,
            NuGetOperationStatus status,
            int packageCount,
            double duration)
        {
            var sortedProjects = projects.OrderBy(
                project => project.GetMetadata<string>(NuGetProjectMetadataKeys.UniqueName));

            var projectIds = sortedProjects.Select(
                project => project.GetMetadata<string>(NuGetProjectMetadataKeys.ProjectId)).ToArray();

            return new VSActionsTelemetryEvent(
                operationId,
                projectIds,
                operationType,
                source,
                startTime,
                status,
                packageCount,
                DateTimeOffset.Now,
                duration);
        }

        public static async Task<ProjectTelemetryEvent> GetProjectTelemetryEventAsync(NuGetProject nuGetProject)
        {
            // Get the project details.
            var projectUniqueName = nuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.UniqueName);

            // Emit the project information.
            try
            {
                var projectId = nuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.ProjectId);

                // Get project type.
                var projectType = NuGetProjectType.Unknown;
                if (nuGetProject is MSBuildNuGetProject)
                {
                    projectType = NuGetProjectType.PackagesConfig;
                }
#if VS15
                else if (nuGetProject is NetCorePackageReferenceProject)
                {
                    projectType = NuGetProjectType.CPSBasedPackageRefs;
                }
                else if (nuGetProject is LegacyPackageReferenceProject)
                {
                    projectType = NuGetProjectType.LegacyProjectSystemWithPackageRefs;
                }
#endif
                else if (nuGetProject is ProjectJsonNuGetProject)
                {
                    projectType = NuGetProjectType.UwpProjectJson;
                }
                else if (nuGetProject is ProjectKNuGetProjectBase)
                {
                    projectType = NuGetProjectType.XProjProjectJson;
                }

                // Get package count.
                var installedPackages = await nuGetProject.GetInstalledPackagesAsync(CancellationToken.None);
                var installedPackagesCount = installedPackages.Count();

                return new ProjectTelemetryEvent(
                    NuGetVersion.Value,
                    projectId,
                    projectType,
                    installedPackagesCount);
            }
            catch (Exception ex)
            {
                var message =
                    $"Failed to emit project information for project '{projectUniqueName}'. Exception:" +
                    Environment.NewLine +
                    ex.ToString();

                ActivityLog.LogWarning(ExceptionHelper.LogEntrySource, message);
                Debug.Fail(message);
                return null;
            }
        }

        public static TelemetryEvent GetUpgradeTelemetryEvent(
            IEnumerable<NuGetProject> projects,
            NuGetOperationStatus status,
            int packageCount)
        {
            var eventName = "UpgradeInformation";

            var sortedProjects = projects.OrderBy(
                project => project.GetMetadata<string>(NuGetProjectMetadataKeys.UniqueName));

            var projectIds = sortedProjects.Select(
                project => project.GetMetadata<string>(NuGetProjectMetadataKeys.ProjectId)).ToArray();

            var telemetryEvent = new TelemetryEvent(eventName);

            telemetryEvent["ProjectIds"] = string.Join(",", projectIds);
            telemetryEvent["Status"] = status;
            telemetryEvent["PackageCount"] = packageCount;

            return telemetryEvent;
        }
    }
}
