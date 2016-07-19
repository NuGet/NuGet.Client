// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Common;
using NuGet.VisualStudio.Facade.Telemetry;

namespace NuGet.PackageManagement.Telemetry
{
    public class NuGetTelemetryService
    {
        public static NuGetTelemetryService Instance = new NuGetTelemetryService(new TelemetrySession());

        private const string EventNamePrefix = "VS/NuGet/";
        private const string PropertyNamePrefix = "VS.NuGet.";

        private const string NuGetVersionPropertyName = PropertyNamePrefix + "NuGetVersion";
        private const string ProjectIdPropertyName = PropertyNamePrefix + "ProjectId";

        private const string ProjectInformationEventName = EventNamePrefix + "ProjectInformation";
        private const string NuGetProjectTypePropertyName = PropertyNamePrefix + "NuGetProjectType";
        
        private const string ProjectDependencyStatisticsEventName = EventNamePrefix + "DependencyStatistics";
        private const string InstalledPackageCountPropertyName = PropertyNamePrefix + "InstalledPackageCount";

        private readonly ITelemetrySession _telemetrySession;
        private readonly string _nuGetVersion;

        public NuGetTelemetryService(ITelemetrySession telemetrySession)
        {
            _nuGetVersion = ClientVersionUtility.GetNuGetAssemblyVersion();
            _telemetrySession = telemetrySession;
        }

        public void EmitProjectInformation(ProjectInformation projectInformation)
        {
            EmitEvent(
                ProjectInformationEventName,
                projectInformation,
                new Dictionary<string, object>
                {
                    { NuGetProjectTypePropertyName, (int) projectInformation.NuGetProjectType }
                });
        }

        public void EmitProjectDependencyStatistics(ProjectDependencyStatistics projectDependencyStatistics)
        {
            EmitEvent(
                ProjectDependencyStatisticsEventName,
                projectDependencyStatistics,
                new Dictionary<string, object>
                {
                    { InstalledPackageCountPropertyName, projectDependencyStatistics.InstalledPackageCount }
                });
        }

        private void EmitEvent(string eventName, ProjectTelemetryEvent projectTelemetryEvent, Dictionary<string, object> properties)
        {
            var telemetryEvent = new TelemetryEvent(eventName);

            foreach (var pair in properties)
            {
                telemetryEvent.Properties[pair.Key] = pair.Value;
            }

            telemetryEvent.Properties[NuGetVersionPropertyName] = projectTelemetryEvent.NuGetVersion;
            telemetryEvent.Properties[ProjectIdPropertyName] = projectTelemetryEvent.ProjectId.ToString();

            _telemetrySession.PostEvent(telemetryEvent);
        }
    }

}
