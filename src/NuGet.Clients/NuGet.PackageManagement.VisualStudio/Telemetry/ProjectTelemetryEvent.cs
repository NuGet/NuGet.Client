// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Common;

namespace NuGet.PackageManagement.Telemetry
{
    public class ProjectTelemetryEvent : INuGetTelemetryEvent
    {
        public ProjectTelemetryEvent(
            string nuGetVersion,
            string projectId,
            NuGetProjectType nuGetProjectType,
            int installedPackageCount)
        {
            NuGetVersion = nuGetVersion;
            ProjectId = projectId;
            NuGetProjectType = nuGetProjectType;
            InstalledPackageCount = installedPackageCount;
        }

        public const string ProjectInformationEventName = "ProjectInformation";

        /// <summary>
        /// The version of NuGet that emitted this event.
        /// </summary>
        public string NuGetVersion { get; }

        /// <summary>
        /// The project ID related to this event.
        /// </summary>
        public string ProjectId { get; }

        /// <summary>
        /// The type of NuGet project this project is.
        /// </summary>
        public NuGetProjectType NuGetProjectType { get; }

        /// <summary>
        /// The number of NuGet packages installed to this project.
        /// </summary>
        public int InstalledPackageCount { get; }

        public TelemetryEvent ToTelemetryEvent(string operationIdPropertyName, string operationId)
        {
            return new TelemetryEvent(
                ProjectInformationEventName,
                new Dictionary<string, object>
                {
                    { nameof(InstalledPackageCount), InstalledPackageCount },
                    { nameof(NuGetProjectType), NuGetProjectType },
                    { nameof(NuGetVersion), NuGetVersion },
                    { nameof(ProjectId), ProjectId.ToString() }
                });
        }
    }
}
