// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using NuGet.Common;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.VisualStudio.Facade.Telemetry;
using EnvDTEProject = EnvDTE.Project;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.Telemetry
{
    /// <summary>
    /// An implementation which emits all of the proper events given a <see cref="ProjectDetails"/> (VS)
    /// and its corresponding <see cref="NuGetProject"/> (NuGet). It extracts metadata from the two
    /// project objects and emits any necessary events.
    /// </summary>
    public class NuGetProjectTelemetryService
    {
        private static readonly Lazy<string> NuGetVersion
            = new Lazy<string>(() => ClientVersionUtility.GetNuGetAssemblyVersion());

        public static NuGetProjectTelemetryService Instance =
            new NuGetProjectTelemetryService(TelemetrySession.Instance);

        private readonly ITelemetrySession telemetrySession;

        public NuGetProjectTelemetryService(ITelemetrySession telemetryService)
        {
            if (telemetryService == null)
            {
                throw new ArgumentNullException(nameof(telemetryService));
            }

            telemetrySession = telemetryService;
        }

        public void EmitNuGetProject(NuGetProject nuGetProject)
        {
            if (nuGetProject == null)
            {
                throw new ArgumentNullException(nameof(nuGetProject));
            }

            // Fire and forget. Emit the events.
            Task.Run(() => EmitNuGetProjectAsync(nuGetProject));
        }

        private async Task EmitNuGetProjectAsync(NuGetProject nuGetProject)
        {
            // Get the project details.
            var projectUniqueName = nuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.UniqueName);
            var projectId = nuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.ProjectId);

            // Emit the project information.
            try
            {
                var projectType = NuGetProjectType.Unknown;
                if (nuGetProject is MSBuildNuGetProject)
                {
                    projectType = NuGetProjectType.PackagesConfig;
                }
                else if (nuGetProject is BuildIntegratedNuGetProject)
                {
                    projectType = NuGetProjectType.UwpProjectJson;
                }
                else if (nuGetProject is ProjectKNuGetProjectBase)
                {
                    projectType = NuGetProjectType.XProjProjectJson;
                }
                else if(nuGetProject is MSBuildShellOutNuGetProject)
                {
                    projectType = NuGetProjectType.CPSBasedPackageRefs;
                }

                var projectInformation = new ProjectInformation(
                    NuGetVersion.Value,
                    projectId,
                    projectType);

                EmitProjectInformation(projectInformation);
            }
            catch (Exception ex)
            {
                var message =
                    $"Failed to emit project information for project '{projectUniqueName}'. Exception:" +
                    Environment.NewLine +
                    ex.ToString();

                ActivityLog.LogWarning(ExceptionHelper.LogEntrySource, message);
                Debug.Fail(message);
            }

            // Emit the project dependency statistics.
            try
            {
                var installedPackages = await nuGetProject.GetInstalledPackagesAsync(CancellationToken.None);
                var installedPackagesCount = installedPackages.Count();

                var projectDependencyStatistics = new ProjectDependencyStatistics(
                    NuGetVersion.Value,
                    projectId,
                    installedPackagesCount);

                EmitProjectDependencyStatistics(projectDependencyStatistics);
            }
            catch (Exception ex)
            {
                var message =
                    $"Failed to emit project dependency statistics for project '{projectUniqueName}'. Exception:" +
                    Environment.NewLine +
                    ex.ToString();

                ActivityLog.LogWarning(message, ExceptionHelper.LogEntrySource);
                Debug.Fail(message);
            }
        }

        public void EmitProjectInformation(ProjectInformation projectInformation)
        {
            var telemetryEvent = new TelemetryEvent(
                TelemetryConstants.ProjectInformationEventName,
                new Dictionary<string, object>
                {
                    { TelemetryConstants.NuGetProjectTypePropertyName, projectInformation.NuGetProjectType },
                    { TelemetryConstants.NuGetVersionPropertyName, projectInformation.NuGetVersion },
                    { TelemetryConstants.ProjectIdPropertyName, projectInformation.ProjectId.ToString() }
                });
            telemetrySession.PostEvent(telemetryEvent);
        }

        public void EmitProjectDependencyStatistics(ProjectDependencyStatistics projectDependencyStatistics)
        {
            var telemetryEvent = new TelemetryEvent(
                TelemetryConstants.ProjectDependencyStatisticsEventName,
                new Dictionary<string, object>
                {
                    { TelemetryConstants.InstalledPackageCountPropertyName, projectDependencyStatistics.InstalledPackageCount },
                    { TelemetryConstants.NuGetVersionPropertyName, projectDependencyStatistics.NuGetVersion },
                    { TelemetryConstants.ProjectIdPropertyName, projectDependencyStatistics.ProjectId.ToString() }
                });
            telemetrySession.PostEvent(telemetryEvent);
        }

    }
}
