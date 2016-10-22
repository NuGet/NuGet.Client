// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.PackageManagement.Telemetry
{
    public class ProjectDependencyStatistics : ProjectTelemetryEvent
    {
        public ProjectDependencyStatistics(
            string nuGetVersion,
            string projectId,
            int installedPackageCount)
            : base(nuGetVersion, projectId)
        {
            InstalledPackageCount = installedPackageCount;
        }

        /// <summary>
        /// The number of NuGet packages installed to this project.
        /// </summary>
        public int InstalledPackageCount { get; }
    }
}
