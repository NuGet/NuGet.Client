// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.PackageManagement.Telemetry;
using NuGet.PackageManagement.VisualStudio;
using NuGetVSExtension;
using Xunit;

namespace NuGet.Tools.Test
{
    public class CopilotToolSessionResultTelemetryEventTests
    {
        public static IEnumerable<object[]> OriginsToolsAndErrorTypes()
        {
            (NavigationOrigin Origin, string ToolName)[] tools =
            {
                (NavigationOrigin.Options_PackageSourceMapping_Review, McpServerConstants.PackageSourceMappingToolName),
                (NavigationOrigin.VulnerabilityInfoBar_FixVulnerabilitiesWithCopilot, McpServerConstants.NuGetSolverToolName),
                (NavigationOrigin.ErrorList_FixVulnerabilitiesWithCopilot, McpServerConstants.NuGetSolverToolName),
            };

            foreach ((NavigationOrigin origin, string toolName) in tools)
            {
                foreach (CopilotToolSessionError errorType in Enum.GetValues(typeof(CopilotToolSessionError)).Cast<CopilotToolSessionError>())
                {
                    yield return new object[] { origin, toolName, errorType };
                }
            }
        }

        [Theory]
        [MemberData(nameof(OriginsToolsAndErrorTypes))]
        public void Constructor_WithOriginToolAndError_CreatesEventWithoutPii(
            NavigationOrigin navigationOrigin,
            string toolName,
            CopilotToolSessionError errorType)
        {
            // Act
            var telemetryEvent = new CopilotToolSessionResultTelemetryEvent(navigationOrigin, toolName, errorType);

            // Assert
            Assert.Equal(CopilotToolSessionResultTelemetryEvent.CopilotToolSessionResultEventName, telemetryEvent.Name);
            Assert.Equal(3, telemetryEvent.Count);
            Assert.Equal(navigationOrigin, telemetryEvent[CopilotToolSessionResultTelemetryEvent.OriginPropertyName]);
            Assert.Equal(toolName, telemetryEvent[CopilotToolSessionResultTelemetryEvent.ToolNamePropertyName]);
            Assert.Equal(errorType, telemetryEvent[CopilotToolSessionResultTelemetryEvent.ErrorTypePropertyName]);
            Assert.Empty(telemetryEvent.GetPiiData());
        }
    }
}
