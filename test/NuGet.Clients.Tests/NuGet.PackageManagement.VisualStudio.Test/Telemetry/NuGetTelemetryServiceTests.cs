// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using NuGet.PackageManagement.Telemetry;
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
                3);
            var target = new NuGetProjectTelemetryService(telemetrySession.Object);

            // Act
            target.EmitProjectInformation(projectInformation);

            // Assert
            telemetrySession.Verify(x => x.PostEvent(It.IsAny<TelemetryEvent>()), Times.Once);
            Assert.NotNull(lastTelemetryEvent);
            Assert.Equal("VS/NuGet/ProjectInformation", lastTelemetryEvent.Name);
            Assert.Equal(4, lastTelemetryEvent.Properties.Count);

            object nuGetVersion;
            Assert.True(lastTelemetryEvent.Properties.TryGetValue("VS.NuGet.NuGetVersion", out nuGetVersion));
            Assert.IsType<string>(nuGetVersion);
            Assert.Equal(projectInformation.NuGetVersion, nuGetVersion);

            object projectId;
            Assert.True(lastTelemetryEvent.Properties.TryGetValue("VS.NuGet.ProjectId", out projectId));
            Assert.IsType<string>(projectId);
            Assert.Equal(projectInformation.ProjectId.ToString(), projectId);

            object actualProjectType;
            Assert.True(lastTelemetryEvent.Properties.TryGetValue("VS.NuGet.NuGetProjectType", out actualProjectType));
            Assert.IsType<NuGetProjectType>(actualProjectType);
            Assert.Equal(projectInformation.NuGetProjectType, actualProjectType);

            object installedPackageCount;
            Assert.True(lastTelemetryEvent.Properties.TryGetValue("VS.NuGet.InstalledPackageCount", out installedPackageCount));
            Assert.IsType<int>(installedPackageCount);
            Assert.Equal(projectInformation.InstalledPackageCount, installedPackageCount);
        }
    }
}
