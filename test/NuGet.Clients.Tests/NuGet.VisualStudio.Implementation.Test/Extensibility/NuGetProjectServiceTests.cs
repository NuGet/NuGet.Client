// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio;
using NuGet.PackageManagement.VisualStudio.Exceptions;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.VisualStudio.Contracts;
using NuGet.VisualStudio.Implementation.Extensibility;
using NuGet.VisualStudio.Telemetry;
using Xunit;

namespace NuGet.VisualStudio.Implementation.Test.Extensibility
{
    public class NuGetProjectServiceTests
    {
        [Fact]
        public async Task GetInstalledPackagesAsync_CpsProjectNotNominated_ReturnsProjectNotReadyResult()
        {
            // Arrange
            var projectGuid = Guid.NewGuid();

            var settings = new Mock<ISettings>();
            var telemetryProvider = new Mock<INuGetTelemetryProvider>(MockBehavior.Strict);

            var project = new Mock<BuildIntegratedNuGetProject>();
            project.Setup(p => p.GetPackageSpecsAndAdditionalMessagesAsync(It.IsAny<DependencyGraphCacheContext>()))
                .Throws<ProjectNotNominatedException>();

            var solutionManager = new Mock<IVsSolutionManager>();
            solutionManager.Setup(sm => sm.GetNuGetProjectAsync(projectGuid.ToString()))
                .Returns(() => Task.FromResult<NuGetProject>(project.Object));

            // Act
            var target = new NuGetProjectService(solutionManager.Object, settings.Object, telemetryProvider.Object);
            InstalledPackagesResult actual = await target.GetInstalledPackagesAsync(projectGuid, CancellationToken.None);

            // Assert
            Assert.NotNull(actual);
            Assert.Equal(InstalledPackageResultStatus.ProjectNotReady, actual.Status);
        }
    }
}
