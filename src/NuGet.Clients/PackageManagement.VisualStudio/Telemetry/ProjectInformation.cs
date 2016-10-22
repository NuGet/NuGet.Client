// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.PackageManagement.Telemetry
{
    public class ProjectInformation : ProjectTelemetryEvent
    {
        public ProjectInformation(
            string nuGetVersion,
            string projectId,
            NuGetProjectType nuGetProjectType)
            : base(nuGetVersion, projectId)
        {
            NuGetProjectType = nuGetProjectType;
        }

        /// <summary>
        /// The type of NuGet project this project is.
        /// </summary>
        public NuGetProjectType NuGetProjectType { get; }
    }
}
