// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Common;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.Telemetry
{
    public class UpgradeInformationTelemetryEvent : TelemetryEvent
    {
        public UpgradeInformationTelemetryEvent()
            : base("UpgradeInformation")
        {
        }

        public void SetResult(IEnumerable<NuGetProject> projects, NuGetOperationStatus status, int packageCount)
        {
            var sortedProjects = projects.OrderBy(
                project => project.GetMetadata<string>(NuGetProjectMetadataKeys.UniqueName));

            var projectIds = sortedProjects.Select(
                project => project.GetMetadata<string>(NuGetProjectMetadataKeys.ProjectId)).ToArray();

            base["ProjectIds"] = string.Join(",", projectIds);
            base["Status"] = status;
            base["PackageCount"] = packageCount;
        }
    }
}
