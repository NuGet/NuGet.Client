// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using NuGet.Common;
using NuGet.PackageManagement.Telemetry;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Telemetry;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class NuGetTelemetryServiceTests
    {
        [Theory]
        [InlineData(NuGetProjectType.Unsupported)]
        [InlineData(NuGetProjectType.Unknown)]
        [InlineData(NuGetProjectType.PackagesConfig)]
        [InlineData(NuGetProjectType.UwpProjectJson)]
        [InlineData(NuGetProjectType.XProjProjectJson)]
        [InlineData(NuGetProjectType.CPSBasedPackageRefs)]
        [InlineData(NuGetProjectType.LegacyProjectSystemWithPackageRefs)]
        [InlineData(NuGetProjectType.UnconfiguredNuGetType)]
        public void NuGetTelemetryService_EmitProjectInformation(NuGetProjectType projectType)
        {
            // Arrange
            var telemetrySession = new Mock<ITelemetrySession>();
            TelemetryEvent lastTelemetryEvent = null;
            telemetrySession
                .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => lastTelemetryEvent = x);

            var projectInformation = new ProjectTelemetryEvent(
                "3.5.0-beta2",
                "15e9591f-9391-4ddf-a246-ca9e0351277d",
                projectType,
                true);
            var target = new NuGetVSTelemetryService(telemetrySession.Object);

            // Act
            target.EmitTelemetryEvent(projectInformation);

            // Assert
            telemetrySession.Verify(x => x.PostEvent(It.IsAny<TelemetryEvent>()), Times.Once);
            Assert.NotNull(lastTelemetryEvent);
            Assert.Equal("ProjectInformation", lastTelemetryEvent.Name);
            Assert.Equal(4, lastTelemetryEvent.Count);

            var nuGetVersion = lastTelemetryEvent["NuGetVersion"];
            Assert.NotNull(nuGetVersion);
            Assert.IsType<string>(nuGetVersion);
            Assert.Equal(projectInformation.NuGetVersion, nuGetVersion);

            var projectId = lastTelemetryEvent["ProjectId"];
            Assert.NotNull(projectId);
            Assert.IsType<string>(projectId);
            Assert.Equal(projectInformation.ProjectId.ToString(), projectId);

            var actualProjectType = lastTelemetryEvent["NuGetProjectType"];
            Assert.NotNull(actualProjectType);
            Assert.IsType<NuGetProjectType>(actualProjectType);
            Assert.Equal(projectInformation.NuGetProjectType, actualProjectType);

            var isPRUpgradable = lastTelemetryEvent["IsPRUpgradable"];
            Assert.NotNull(isPRUpgradable);
            Assert.IsType<bool>(isPRUpgradable);
            Assert.Equal(projectInformation.IsProjectPRUpgradable, isPRUpgradable);
        }
    }
}
