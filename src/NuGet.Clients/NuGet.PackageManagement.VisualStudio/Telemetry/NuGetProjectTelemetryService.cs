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
                else if (nuGetProject is CpsPackageReferenceProject)
                {
                    projectType = NuGetProjectType.CPSBasedPackageRefs;
                }
                else if (nuGetProject is LegacyCSProjPackageReferenceProject)
                {
                    projectType = NuGetProjectType.LegacyProjectSystemWithPackageRefs;
                }
#endif
                else if (nuGetProject is ProjectJsonBuildIntegratedProjectSystem)
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

                var projectInformation = new ProjectTelemetryEvent(
                    NuGetVersion.Value,
                    projectId,
                    projectType,
                    installedPackagesCount);

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
        }

        public void EmitProjectInformation(ProjectTelemetryEvent projectInformation)
        {
            var telemetryEvent = new TelemetryEvent(
                TelemetryConstants.ProjectInformationEventName,
                new Dictionary<string, object>
                {
                    { TelemetryConstants.InstalledPackageCountPropertyName, projectInformation.InstalledPackageCount },
                    { TelemetryConstants.NuGetProjectTypePropertyName, projectInformation.NuGetProjectType },
                    { TelemetryConstants.NuGetVersionPropertyName, projectInformation.NuGetVersion },
                    { TelemetryConstants.ProjectIdPropertyName, projectInformation.ProjectId.ToString() }
                });
            telemetrySession.PostEvent(telemetryEvent);
        }
    }
}
