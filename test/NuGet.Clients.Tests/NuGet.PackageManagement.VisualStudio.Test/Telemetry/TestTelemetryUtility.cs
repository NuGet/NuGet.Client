// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Telemetry;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class TestTelemetryUtility
    {
        public static void VerifyTelemetryEventData(string operationId, ActionEventBase expected, TelemetryEvent actual)
        {
            Assert.Equal(operationId, actual["OperationId"].ToString());
            Assert.Equal(expected.ProjectsCount, (int)actual["ProjectsCount"]);
            Assert.Equal(string.Join(",", expected.ProjectIds), actual["ProjectIds"].ToString());
            Assert.Equal(expected.PackagesCount, (int)actual["PackagesCount"]);
            Assert.Equal(expected.Status.ToString(), actual["Status"].ToString());
            Assert.Equal(expected.StartTime, actual["StartTime"]);
            Assert.Equal(expected.EndTime, actual["EndTime"]);
            Assert.Equal(expected.Duration, (double)actual["Duration"]);
        }
    }
}
