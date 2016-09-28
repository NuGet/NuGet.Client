// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using NuGet.Common;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
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

        public static NuGetProjectTelemetryService Instance = new NuGetProjectTelemetryService(
            NuGetTelemetryService.Instance);

        private readonly NuGetTelemetryService _nuGetTelemetryService;

        public NuGetProjectTelemetryService(NuGetTelemetryService telemetryService)
        {
            if (telemetryService == null)
            {
                throw new ArgumentNullException(nameof(telemetryService));
            }

            _nuGetTelemetryService = telemetryService;
        }

        public void EmitNuGetProject(EnvDTEProject vsProject, NuGetProject nuGetProject)
        {
            if (vsProject == null)
            {
                throw new ArgumentNullException(nameof(vsProject));
            }

            if (nuGetProject == null)
            {
                throw new ArgumentNullException(nameof(nuGetProject));
            }

            // Fire and forget. Emit the events.
            Task.Run(() => EmitNuGetProjectAsync(vsProject, nuGetProject));
        }

        private async Task EmitNuGetProjectAsync(EnvDTEProject vsProject, NuGetProject nuGetProject)
        {
            // Get the project details.
            var projectUniqueName = nuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.UniqueName);
            var projectId = Guid.Empty;
            try
            {
                projectId = await ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    
                    // Get the project ID.
                    var vsHierarchyItem = VsHierarchyUtility.GetHierarchyItemForProject(vsProject);
                    Guid id;
                    if (!vsHierarchyItem.TryGetProjectId(out id))
                    {
                        id = Guid.Empty;
                    }

                    return id;
                });
            }
            catch (Exception ex)
            {
                var message =
                    $"Failed to get project name or project ID. Exception:" +
                    Environment.NewLine +
                    ex.ToString();

                ActivityLog.LogWarning(ExceptionHelper.LogEntrySource, message);
                Debug.Fail(message);

                return;
            }
            
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
                else if(nuGetProject is CpsPackageReferenceProject)
                {
                    projectType = NuGetProjectType.CPSBasedPackageRefs;
                }

                var projectInformation = new ProjectInformation(
                    NuGetVersion.Value,
                    projectId,
                    projectType);

                _nuGetTelemetryService.EmitProjectInformation(projectInformation);
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

                _nuGetTelemetryService.EmitProjectDependencyStatistics(projectDependencyStatistics);
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
    }
}
