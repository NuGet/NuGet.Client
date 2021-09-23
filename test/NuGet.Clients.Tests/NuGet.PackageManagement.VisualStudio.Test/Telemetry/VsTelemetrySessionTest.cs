// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.Telemetry;
using NuGet.Common;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Telemetry;
using Xunit;
using TelemetryEvent = NuGet.Common.TelemetryEvent;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class VsTelemetrySessionTest
    {
        [Fact]
        public void VsTelemetrySession_ToVsTelemetryEvent()
        {
            // Arrange
            var actionTelemetryData = new VSActionsTelemetryEvent(
               Guid.NewGuid().ToString(),
               projectIds: new[] { Guid.NewGuid().ToString() },
               operationType: NuGetOperationType.Install,
               source: OperationSource.UI,
               startTime: DateTimeOffset.Now.AddSeconds(-2),
               status: NuGetOperationStatus.Failed,
               packageCount: 1,
               endTime: DateTimeOffset.Now,
               duration: 1.30,
               isPackageSourceMappingEnabled: false);

            // Act
            var vsTelemetryEvent = VSTelemetrySession.ToVsTelemetryEvent(actionTelemetryData);

            // Assert
            Assert.True(vsTelemetryEvent.Name.StartsWith(VSTelemetrySession.VSEventNamePrefix, ignoreCase: true, culture: CultureInfo.InvariantCulture));
            Assert.True(vsTelemetryEvent.Properties.Keys.All(
                p => p.StartsWith(VSTelemetrySession.VSPropertyNamePrefix, ignoreCase: true, culture: CultureInfo.InvariantCulture)));
        }

        [Fact]
        public void ToComplexProperty_StringListComplexProperty_RoundTrips()
        {
            // Arrange
            var stringList = new List<string>()
            {
                "a", "list", "of", "strings"
            };

            var telObject = new TelemetryEvent("test/event/name");
            telObject.ComplexData["myListOfStrings"] = stringList;

            // Act
            var vsTelemetryEvent = VSTelemetrySession.ToVsTelemetryEvent(telObject);

            Assert.NotNull(vsTelemetryEvent.Properties["VS.NuGet.myListOfStrings"] as TelemetryComplexProperty);
            object value = vsTelemetryEvent.Properties["VS.NuGet.myListOfStrings"];

            var prop = value as TelemetryComplexProperty;

            Assert.Collection(prop.Value as List<object>,
                item1 => AssertElement(item1, "a"),
                item2 => AssertElement(item2, "list"),
                item3 => AssertElement(item3, "of"),
                item4 => AssertElement(item4, "strings"));
        }

        private static void AssertElement(object element, string expected)
        {
            Assert.IsType<string>(element);
            Assert.Equal(expected, element);
        }
    }
}
