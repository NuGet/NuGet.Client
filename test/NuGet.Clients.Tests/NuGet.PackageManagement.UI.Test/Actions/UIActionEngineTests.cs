// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    public class UIActionEngineTests
    {
        [Fact]
        public async Task GetPreviewResultsAsync_WhenPackageIdentityIsSubclass_ItIsReplacedWithNewPackageIdentity()
        {
            string projectId = Guid.NewGuid().ToString();
            var packageIdentityA1 = new PackageIdentitySubclass(id: "a", NuGetVersion.Parse("1.0.0"));
            var packageIdentityA2 = new PackageIdentitySubclass(id: "a", NuGetVersion.Parse("2.0.0"));
            var packageIdentityB1 = new PackageIdentitySubclass(id: "b", NuGetVersion.Parse("3.0.0"));
            var packageIdentityB2 = new PackageIdentitySubclass(id: "b", NuGetVersion.Parse("4.0.0"));
            var uninstallAction = new ProjectAction(
                id: Guid.NewGuid().ToString(),
                projectId,
                packageIdentityA1,
                NuGetProjectActionType.Uninstall,
                implicitActions: new[]
                {
                    new ImplicitProjectAction(
                        id: Guid.NewGuid().ToString(),
                        packageIdentityA1,
                        NuGetProjectActionType.Uninstall),
                    new ImplicitProjectAction(
                        id: Guid.NewGuid().ToString(),
                        packageIdentityB1,
                        NuGetProjectActionType.Uninstall)
                });
            var installAction = new ProjectAction(
                id: Guid.NewGuid().ToString(),
                projectId,
                packageIdentityA2,
                NuGetProjectActionType.Install,
                implicitActions: new[]
                {
                    new ImplicitProjectAction(
                        id: Guid.NewGuid().ToString(),
                        packageIdentityA2,
                        NuGetProjectActionType.Install),
                    new ImplicitProjectAction(
                        id: Guid.NewGuid().ToString(),
                        packageIdentityB2,
                        NuGetProjectActionType.Install)
                });
            IReadOnlyList<PreviewResult> previewResults = await UIActionEngine.GetPreviewResultsAsync(
                Mock.Of<INuGetProjectManagerService>(),
                new[] { uninstallAction, installAction },
                CancellationToken.None);

            Assert.Equal(1, previewResults.Count);
            UpdatePreviewResult[] updatedResults = previewResults[0].Updated.ToArray();

            Assert.Equal(2, updatedResults.Length);

            UpdatePreviewResult updatedResult = updatedResults[0];

            Assert.False(updatedResult.Old.GetType().IsSubclassOf(typeof(PackageIdentity)));
            Assert.False(updatedResult.New.GetType().IsSubclassOf(typeof(PackageIdentity)));
            Assert.Equal("a.1.0.0 -> a.2.0.0", updatedResult.ToString());

            updatedResult = updatedResults[1];

            Assert.False(updatedResult.Old.GetType().IsSubclassOf(typeof(PackageIdentity)));
            Assert.False(updatedResult.New.GetType().IsSubclassOf(typeof(PackageIdentity)));
            Assert.Equal("b.3.0.0 -> b.4.0.0", updatedResult.ToString());
        }

        [Fact]
        public async Task GetPreviewResultsAsync_WithMultipleActions_SortsPackageIdentities()
        {
            string projectId = Guid.NewGuid().ToString();
            var packageIdentityA = new PackageIdentitySubclass(id: "a", NuGetVersion.Parse("1.0.0"));
            var packageIdentityB = new PackageIdentitySubclass(id: "b", NuGetVersion.Parse("2.0.0"));
            var packageIdentityC = new PackageIdentitySubclass(id: "c", NuGetVersion.Parse("3.0.0"));
            var installAction = new ProjectAction(
                id: Guid.NewGuid().ToString(),
                projectId,
                packageIdentityB,
                NuGetProjectActionType.Install,
                implicitActions: new[]
                {
                    new ImplicitProjectAction(
                        id: Guid.NewGuid().ToString(),
                        packageIdentityA,
                        NuGetProjectActionType.Install),
                    new ImplicitProjectAction(
                        id: Guid.NewGuid().ToString(),
                        packageIdentityB,
                        NuGetProjectActionType.Install),
                    new ImplicitProjectAction(
                        id: Guid.NewGuid().ToString(),
                        packageIdentityC,
                        NuGetProjectActionType.Install)
                });
            IReadOnlyList<PreviewResult> previewResults = await UIActionEngine.GetPreviewResultsAsync(
                Mock.Of<INuGetProjectManagerService>(),
                new[] { installAction },
                CancellationToken.None);

            Assert.Equal(1, previewResults.Count);
            AccessiblePackageIdentity[] addedResults = previewResults[0].Added.ToArray();

            Assert.Equal(3, addedResults.Length);

            Assert.Equal(packageIdentityA.Id, addedResults[0].Id);
            Assert.Equal(packageIdentityB.Id, addedResults[1].Id);
            Assert.Equal(packageIdentityC.Id, addedResults[2].Id);
        }

        [Fact]
        public void AddUiActionEngineTelemetryProperties_AddsVulnerabilityInfo_Succeeds()
        {
            // Arrange
            var telemetrySession = new Mock<ITelemetrySession>();
            TelemetryEvent lastTelemetryEvent = null;
            telemetrySession
                .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => lastTelemetryEvent = x);

            var operationId = Guid.NewGuid().ToString();

            var actionTelemetryData = new VSActionsTelemetryEvent(
                operationId,
                projectIds: new[] { Guid.NewGuid().ToString() },
                operationType: NuGetOperationType.Install,
                source: OperationSource.PMC,
                startTime: DateTimeOffset.Now.AddSeconds(-1),
                status: NuGetOperationStatus.NoOp,
                packageCount: 1,
                endTime: DateTimeOffset.Now,
                duration: .40,
                isPackageSourceMappingEnabled: false);

            UIActionEngine.AddUiActionEngineTelemetryProperties(
                actionTelemetryEvent: actionTelemetryData,
                continueAfterPreview: true,
                acceptedLicense: true,
                userAction: UserAction.CreateInstallAction("mypackageId", new NuGetVersion(1, 0, 0)),
                selectedIndex: 0,
                recommendedCount: 0,
                recommendPackages: false,
                recommenderVersion: null,
                topLevelVulnerablePackagesCount: 3,
                topLevelVulnerablePackagesMaxSeverities: new List<int> { 1, 1, 3 }, // each package has its own max severity
                existingPackages: null,
                addedPackages: null,
                removedPackages: null,
                updatedPackagesOld: null,
                updatedPackagesNew: null,
                targetFrameworks: null);

            // Act
            var service = new NuGetVSTelemetryService(telemetrySession.Object);
            service.EmitTelemetryEvent(actionTelemetryData);

            // Assert
            Assert.NotNull(lastTelemetryEvent);
            Assert.NotNull(lastTelemetryEvent.ComplexData["TopLevelVulnerablePackagesMaxSeverities"] as List<int>);
            var pkgSeverities = lastTelemetryEvent.ComplexData["TopLevelVulnerablePackagesMaxSeverities"] as List<int>;
            Assert.Equal(lastTelemetryEvent["TopLevelVulnerablePackagesCount"], pkgSeverities.Count());
            Assert.Collection(pkgSeverities,
                item => Assert.Equal(1, item),
                item => Assert.Equal(1, item),
                item => Assert.Equal(3, item));
            Assert.Equal(3, pkgSeverities.Count());
        }

        private sealed class PackageIdentitySubclass : PackageIdentity
        {
            public PackageIdentitySubclass(string id, NuGetVersion version)
                : base(id, version)
            {
            }

            public override string ToString()
            {
                return "If this displays, it is a bug";
            }
        }
    }
}
