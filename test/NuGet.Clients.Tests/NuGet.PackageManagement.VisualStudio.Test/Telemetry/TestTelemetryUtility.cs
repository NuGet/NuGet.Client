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
            Assert.Equal(operationId, actual.Properties["OperationId"].ToString());
            Assert.Equal(expected.ProjectsCount, (int)actual.Properties["ProjectsCount"]);
            Assert.Equal(string.Join(",", expected.ProjectIds), actual.Properties["ProjectIds"].ToString());
            Assert.Equal(expected.PackagesCount, (int)actual.Properties["PackagesCount"]);
            Assert.Equal(expected.Status.ToString(), actual.Properties["Status"].ToString());
            Assert.Equal(expected.StartTime.ToString(), actual.Properties["StartTime"].ToString());
            Assert.Equal(expected.EndTime.ToString(), actual.Properties["EndTime"].ToString());
            Assert.Equal(expected.Duration, (double)actual.Properties["Duration"]);
        }
    }
}
