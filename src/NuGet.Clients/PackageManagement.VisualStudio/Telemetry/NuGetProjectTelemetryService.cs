// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using NuGet.Common;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;

namespace NuGet.PackageManagement.Telemetry
{
    public class NuGetProjectTelemetryService
    {
        public static NuGetProjectTelemetryService Instance = new NuGetProjectTelemetryService(
            NuGetTelemetryService.Instance);

        private readonly NuGetTelemetryService _nuGetTelemetryService;
        private readonly string _nuGetVersion;

        public NuGetProjectTelemetryService(NuGetTelemetryService telemetryService)
        {
            _nuGetVersion = ClientVersionUtility.GetNuGetAssemblyVersion();
            _nuGetTelemetryService = telemetryService;
        }

        public void EmitNuGetProject(Project vsProject, NuGetProject nuGetProject, CancellationToken token)
        {
            // Get the project ID.
            var vsHierarchyItem = VsHierarchyUtility.GetHierarchyItemForProject(vsProject);
            Guid projectId;
            if (!vsHierarchyItem.TryGetProjectId(out projectId))
            {
                projectId = Guid.Empty;
            }

            // Emit the events.
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
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
                        projectType = NuGetProjectType.NetCoreProjectJson;
                    }

                    var projectInformation = new ProjectInformation(
                        _nuGetVersion,
                        projectId,
                        projectType);

                    _nuGetTelemetryService.EmitProjectInformation(projectInformation);
                }
                catch
                {
                    // Ignore failures.
                }

                // Emit the project dependency statistics.
                try
                {
                    var installedPackages = await nuGetProject.GetInstalledPackagesAsync(token);
                    var installedPackagesCount = installedPackages.Count();

                    var projectDependencyStatistics = new ProjectDependencyStatistics(
                        _nuGetVersion,
                        projectId,
                        installedPackagesCount);

                    _nuGetTelemetryService.EmitProjectDependencyStatistics(projectDependencyStatistics);
                }
                catch
                {
                    // Ignore failures.
                }
            });
        }
    }
}
