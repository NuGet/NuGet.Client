// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Common;

namespace NuGet.PackageManagement.Telemetry
{
    public class ProjectTelemetryEvent : TelemetryEvent
    {
        public ProjectTelemetryEvent(
            string nuGetVersion,
            string projectId,
            NuGetProjectType nuGetProjectType,
            int installedPackageCount) :
            base(ProjectInformationEventName, new Dictionary<string, object>
                {
                    { nameof(InstalledPackageCount), installedPackageCount },
                    { nameof(NuGetProjectType), nuGetProjectType },
                    { nameof(NuGetVersion), nuGetVersion },
                    { nameof(ProjectId), projectId.ToString() }
                })
        {
        }

        public const string ProjectInformationEventName = "ProjectInformation";

        /// <summary>
        /// The version of NuGet that emitted this event.
        /// </summary>
        public string NuGetVersion => (string)base[nameof(NuGetVersion)];

        /// <summary>
        /// The project ID related to this event.
        /// </summary>
        public string ProjectId => (string)base[nameof(ProjectId)];

        /// <summary>
        /// The type of NuGet project this project is.
        /// </summary>
        public NuGetProjectType NuGetProjectType => (NuGetProjectType)base[nameof(NuGetProjectType)];

        /// <summary>
        /// The number of NuGet packages installed to this project.
        /// </summary>
        public int InstalledPackageCount => (int)base[nameof(InstalledPackageCount)];
    }
}
