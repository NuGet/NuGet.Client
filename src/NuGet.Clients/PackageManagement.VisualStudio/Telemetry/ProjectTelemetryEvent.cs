// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.PackageManagement.Telemetry
{
    public abstract class ProjectTelemetryEvent
    {
        public ProjectTelemetryEvent(string nuGetVersion, string projectId)
        {
            NuGetVersion = nuGetVersion;
            ProjectId = projectId;
        }

        /// <summary>
        /// The version of NuGet that emitted this event.
        /// </summary>
        public string NuGetVersion { get; }

        /// <summary>
        /// The project ID related to this event.
        /// </summary>
        public string ProjectId { get; }
    }
}
