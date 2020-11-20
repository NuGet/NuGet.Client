// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;

namespace NuGet.SolutionRestoreManager
{
    class RestoreStartEvent : TelemetryEvent
    {
        public RestoreStartEvent(
            string eventName,
            List<string> projectIds,
            DateTimeOffset startTime,
            string restoreReason
            ) :
            base(eventName, new Dictionary<string, object>
                {
                    { nameof(RestoreReason), restoreReason },
                    { nameof(StartTime), startTime.UtcDateTime.ToString("O") },
                    { nameof(ProjectsCount), projectIds.Count }
                })
        {
            ProjectIds = projectIds;

            // log each project id separately so that it can be joined with ProjectInformation telemetry event
            for (var i = 0; i < projectIds.Count; i++)
            {
                this[$"ProjectId{i + 1}"] = projectIds[i];
            }
        }

        public string RestoreReason => (string)base[nameof(RestoreReason)];

        public List<string> ProjectIds;

        public int ProjectsCount => (int)base[nameof(ProjectsCount)];

        public string StartTime => (string)base[nameof(StartTime)];
    }
}
