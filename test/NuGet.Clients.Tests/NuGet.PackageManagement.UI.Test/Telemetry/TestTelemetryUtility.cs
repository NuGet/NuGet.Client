// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.ProjectManagement;
using NuGet.VisualStudio.Facade.Telemetry;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    public class TestTelemetryUtility
    {
        public static void VerifyTelemetryEventData(ActionEventBase expected, TelemetryEvent actual)
        {
            Assert.Equal(expected.OperationId, actual.Properties[TelemetryConstants.OperationIdPropertyName].ToString());
            Assert.Equal(string.Join(",", expected.ProjectIds), actual.Properties[TelemetryConstants.ProjectIdsPropertyName].ToString());
            Assert.Equal(expected.PackagesCount, (int)actual.Properties[TelemetryConstants.PackagesCountPropertyName]);
            Assert.Equal(expected.Status.ToString(), actual.Properties[TelemetryConstants.OperationStatusPropertyName].ToString());
            Assert.Equal(expected.StartTime.ToString(), actual.Properties[TelemetryConstants.StartTimePropertyName].ToString());
            Assert.Equal(expected.EndTime.ToString(), actual.Properties[TelemetryConstants.EndTimePropertyName].ToString());
            Assert.Equal(expected.Duration, (double)actual.Properties[TelemetryConstants.DurationPropertyName]);
        }
    }
}
