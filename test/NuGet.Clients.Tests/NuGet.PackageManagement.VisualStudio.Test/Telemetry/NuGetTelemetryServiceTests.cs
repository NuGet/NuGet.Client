using System;
using Moq;
using NuGet.PackageManagement.Telemetry;
using NuGet.VisualStudio.Facade.Telemetry;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test.Telemetry
{
    public class NuGetTelemetryServiceTests
    {
        [Theory]
        [InlineData(NuGetProjectType.Unsupported, 0)]
        [InlineData(NuGetProjectType.Unknown, 1)]
        [InlineData(NuGetProjectType.PackagesConfig, 2)]
        [InlineData(NuGetProjectType.UwpProjectJson, 3)]
        [InlineData(NuGetProjectType.XProjProjectJson, 4)]
        public void NuGetTelemetryService_EmitProjectInformation(NuGetProjectType projectType, int expectedProjectType)
        {
            // Arrange
            var telemetrySession = new Mock<ITelemetrySession>();
            TelemetryEvent lastTelemetryEvent = null;
            telemetrySession
                .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => lastTelemetryEvent = x);

            var projectInformation = new ProjectInformation(
                "3.5.0-beta2",
                new Guid("15e9591f-9391-4ddf-a246-ca9e0351277d"),
                projectType);
            var target = new NuGetTelemetryService(telemetrySession.Object);

            // Act
            target.EmitProjectInformation(projectInformation);

            // Assert
            telemetrySession.Verify(x => x.PostEvent(It.IsAny<TelemetryEvent>()), Times.Once);
            Assert.NotNull(lastTelemetryEvent);
            Assert.Equal("VS/NuGet/ProjectInformation", lastTelemetryEvent.Name);
            Assert.Equal(3, lastTelemetryEvent.Properties.Count);

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
            Assert.IsType<int>(actualProjectType);
            Assert.Equal(expectedProjectType, actualProjectType);
        }

        [Fact]
        public void NuGetTelemetryService_EmitProjectDependencyStatistics()
        {
            // Arrange
            var telemetrySession = new Mock<ITelemetrySession>();
            TelemetryEvent lastTelemetryEvent = null;
            telemetrySession
                .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => lastTelemetryEvent = x);

            var projectInformation = new ProjectDependencyStatistics(
                "3.5.0-beta2",
                new Guid("15e9591f-9391-4ddf-a246-ca9e0351277d"),
                3);
            var target = new NuGetTelemetryService(telemetrySession.Object);

            // Act
            target.EmitProjectDependencyStatistics(projectInformation);

            // Assert
            telemetrySession.Verify(x => x.PostEvent(It.IsAny<TelemetryEvent>()), Times.Once);
            Assert.NotNull(lastTelemetryEvent);
            Assert.Equal("VS/NuGet/DependencyStatistics", lastTelemetryEvent.Name);
            Assert.Equal(3, lastTelemetryEvent.Properties.Count);

            object nuGetVersion;
            Assert.True(lastTelemetryEvent.Properties.TryGetValue("VS.NuGet.NuGetVersion", out nuGetVersion));
            Assert.IsType<string>(nuGetVersion);
            Assert.Equal(projectInformation.NuGetVersion, nuGetVersion);

            object projectId;
            Assert.True(lastTelemetryEvent.Properties.TryGetValue("VS.NuGet.ProjectId", out projectId));
            Assert.IsType<string>(projectId);
            Assert.Equal(projectInformation.ProjectId.ToString(), projectId);

            object installedPackageCount;
            Assert.True(lastTelemetryEvent.Properties.TryGetValue("VS.NuGet.InstalledPackageCount", out installedPackageCount));
            Assert.IsType<int>(installedPackageCount);
            Assert.Equal(projectInformation.InstalledPackageCount, installedPackageCount);
        }
    }
}
