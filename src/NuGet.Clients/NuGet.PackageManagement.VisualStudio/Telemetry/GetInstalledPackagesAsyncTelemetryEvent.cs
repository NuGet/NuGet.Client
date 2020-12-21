// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.PackageManagement.Telemetry
{
    public sealed class GetInstalledPackagesAsyncTelemetryEvent : DiagnosticTelemetryEvent
    {
        private const string Data = "Data";

        private static readonly string EventName = $"{DiagnosticEventName}/GetInstalledPackagesAsync";

        public GetInstalledPackagesAsyncTelemetryEvent()
            : base(EventName)
        {
        }

        public void AddProject(NuGetProjectType projectType, string projectId, int nullCount, int totalCount)
        {
            ProjectTypeAndData projectTypeAndData;

            if (ComplexData.TryGetValue(Data, out object value) && value is ProjectTypeAndData data)
            {
                projectTypeAndData = data;
            }
            else
            {
                projectTypeAndData = new ProjectTypeAndData(projectType.ToString());

                ComplexData[Data] = projectTypeAndData;
            }

            projectTypeAndData.Projects.Add(new ProjectData(projectId, nullCount, totalCount));
        }

        private sealed class ProjectTypeAndData
        {
            public string ProjectType { get; }
            public List<ProjectData> Projects { get; }

            internal ProjectTypeAndData(string projectType)
            {
                ProjectType = projectType;
                Projects = new List<ProjectData>();
            }
        }

        private sealed class ProjectData
        {
            public string ProjectId { get; }
            public int NullCount { get; }
            public int TotalCount { get; }

            internal ProjectData(string projectId, int nullCount, int totalCount)
            {
                ProjectId = projectId;
                NullCount = nullCount;
                TotalCount = totalCount;
            }
        }
    }
}
