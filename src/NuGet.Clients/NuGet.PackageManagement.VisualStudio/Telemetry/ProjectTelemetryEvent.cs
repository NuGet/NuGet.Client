// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
            bool isPRUpgradable,
            string fullPath) :
            base(ProjectInformationEventName, new Dictionary<string, object>
                {
                    { nameof(NuGetProjectType), nuGetProjectType },
                    { nameof(NuGetVersion), nuGetVersion },
                    { nameof(ProjectId), projectId },
                    { IsPRUpgradable, isPRUpgradable },
                    { nameof(FullPath), fullPath },
                })
        {
            if (nuGetVersion == null)
            {
                throw new ArgumentNullException(nameof(nuGetVersion));
            }

            if (projectId == null)
            {
                throw new ArgumentNullException(nameof(projectId));
            }

            if (fullPath == null)
            {
                throw new ArgumentNullException(nameof(fullPath));
            }
        }

        public const string ProjectInformationEventName = "ProjectInformation";
        public const string IsPRUpgradable = "IsPRUpgradable";

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
        /// True, if project can be upgraded to PackageReference.
        /// </summary>
        public bool IsProjectPRUpgradable => (bool)base[IsPRUpgradable];

        /// <summary>
        /// Absolute project path
        /// </summary>
        public string FullPath => (string)base[nameof(FullPath)];
    }
}
