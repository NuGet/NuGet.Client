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
        public void AddUiActionEngineTelemetryProperties_AddsDeprecatedPackages_Succeeds()
        {
            // Arrange
            var telemetrySession = new Mock<ITelemetrySession>();
            TelemetryEvent lastTelemetryEvent = null;
            _ = telemetrySession
                .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => lastTelemetryEvent = x);

            // each package has its own max severity
            var mySelectedPackages = new List<PackageItemViewModel>()
            {
                new PackageItemViewModel(null)
                {
                    Id = "HappyPackage",
                    Version = new NuGetVersion(1, 0, 0),
                },
                new PackageItemViewModel(null)
                {
                    Id = "pkgDeprecatedA",
                    Version = new NuGetVersion(1, 0, 0),
                    DeprecationMetadata = new PackageDeprecationMetadataContextInfo(
                        message: "This package is no longer maintained",
                        reasons: new [] {
                            "Liberty",
                            "Equality",
                            "Fraternity"
                        },
                        alternatePackageContextInfo: null),
                },
                new PackageItemViewModel(null)
                {
                    Id = "pkgB",
                    Version = new NuGetVersion(1, 0, 0),
                    Vulnerabilities = new List<PackageVulnerabilityMetadataContextInfo>()
                    {
                        new PackageVulnerabilityMetadataContextInfo(new Uri("https://a.random.uri/info"), 1),
                    },
                    DeprecationMetadata = new PackageDeprecationMetadataContextInfo(
                        message: "This package is obsolete",
                        reasons: new [] {
                            "Liberty",
                            "Equality",
                        },
                        alternatePackageContextInfo: new AlternatePackageMetadataContextInfo("newPackage", VersionRange.All)),
                },
                new PackageItemViewModel(null)
                {
                    Id = "pkgC",
                    Version = new NuGetVersion(1, 0, 0),
                },
            };
            PackageItemViewModel highlightedPackage = mySelectedPackages.First();

            var actionTelemetryData = CreateTestActionTelemetryEvent();

            UIActionEngine.AddUiActionEngineTelemetryProperties(
                actionTelemetryEvent: actionTelemetryData,
                continueAfterPreview: true,
                acceptedLicense: true,
                userAction: UserAction.CreateInstallAction(highlightedPackage.Id, highlightedPackage.Version),
                selectedPackages: mySelectedPackages,
                selectedIndex: null,
                recommendedCount: null,
                recommendPackages: null,
                recommenderVersion: null,
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

            // Deprecation
            Assert.NotNull(lastTelemetryEvent.ComplexData["TopLevelDeprecatedPackages"]);
            Assert.NotNull(lastTelemetryEvent.ComplexData["TopLevelDeprecatedPackages"] as List<TelemetryEvent>);
            var deprecated = lastTelemetryEvent.ComplexData["TopLevelDeprecatedPackages"] as List<TelemetryEvent>;

            Assert.Equal(2, deprecated.Count);

            var first = deprecated[0];

            Assert.NotNull(first);
            Assert.NotNull(first.ComplexData["Reasons"] as List<string>);
            var reasons1 = first.ComplexData["Reasons"] as List<string>;
            Assert.Collection(reasons1,
                item1 => Assert.Equal("Liberty", item1),
                item2 => Assert.Equal("Equality", item2),
                item3 => Assert.Equal("Fraternity", item3));

            var second = deprecated[1];
            Assert.NotNull(second);
            Assert.NotNull(second.ComplexData["Reasons"] as List<string>);
            var reasons2 = second.ComplexData["Reasons"] as List<string>;
            Assert.Collection(reasons2,
                item1 => Assert.Equal("Liberty", item1),
                item2 => Assert.Equal("Equality", item2));
            Assert.NotNull(second.ComplexData["AlternativePackage"] as TelemetryEvent);

            var altPackage = second.ComplexData["AlternativePackage"] as TelemetryEvent;

            Assert.NotNull(altPackage.GetPiiData().Where(x => x.Key == "id").First());
            Assert.NotNull(altPackage["version"]);
        }

        private VSActionsTelemetryEvent CreateTestActionTelemetryEvent()
        {
            return new VSActionsTelemetryEvent(
                operationId: Guid.NewGuid().ToString(),
                projectIds: new[] { Guid.NewGuid().ToString() },
                operationType: NuGetOperationType.Install,
                source: OperationSource.PMC,
                startTime: DateTimeOffset.Now.AddSeconds(-1),
                status: NuGetOperationStatus.NoOp,
                packageCount: 1,
                endTime: DateTimeOffset.Now,
                duration: .40,
                isPackageSourceMappingEnabled: false);
        }

        [Fact]
        public void AddUiActionEngineTelemetryProperties_AddsVulnerabilityInfo_Succeeds()
        {
            // Arrange
            var telemetrySession = new Mock<ITelemetrySession>();
            TelemetryEvent lastTelemetryEvent = null;
            _ = telemetrySession
                .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => lastTelemetryEvent = x);

            // each package has its own max severity
            var mySelectedPackages = new List<PackageItemViewModel>()
            {
                new PackageItemViewModel(null)
                {
                    Id = "NonVulnerablePackge",
                    Version = new NuGetVersion(1, 0, 0),
                },
                new PackageItemViewModel(null)
                {
                    Id = "pkgA",
                    Version = new NuGetVersion(1, 0, 0),
                    Vulnerabilities = new List<PackageVulnerabilityMetadataContextInfo>()
                    {
                        new PackageVulnerabilityMetadataContextInfo(new Uri("https://a.random.uri/info"), 1),
                        new PackageVulnerabilityMetadataContextInfo(new Uri("https://a.random.uri/info"), 1),
                    },
                },
                new PackageItemViewModel(null)
                {
                    Id = "pkgB",
                    Version = new NuGetVersion(1, 0, 0),
                    Vulnerabilities = new List<PackageVulnerabilityMetadataContextInfo>()
                    {
                        new PackageVulnerabilityMetadataContextInfo(new Uri("https://a.random.uri/info"), 1),
                    },
                },
                new PackageItemViewModel(null)
                {
                    Id = "pkgC",
                    Version = new NuGetVersion(1, 0, 0),
                    Vulnerabilities = new List<PackageVulnerabilityMetadataContextInfo>()
                    {
                        new PackageVulnerabilityMetadataContextInfo(new Uri("https://a.random.uri/info"), 1),
                        new PackageVulnerabilityMetadataContextInfo(new Uri("https://a.random.uri/info"), 1),
                        new PackageVulnerabilityMetadataContextInfo(new Uri("https://a.random.uri/info"), 3),
                    },
                },
            };
            PackageItemViewModel highlightedPackage = mySelectedPackages.First();

            var actionTelemetryData = CreateTestActionTelemetryEvent();

            UIActionEngine.AddUiActionEngineTelemetryProperties(
                actionTelemetryEvent: actionTelemetryData,
                continueAfterPreview: true,
                acceptedLicense: true,
                userAction: UserAction.CreateInstallAction(highlightedPackage.Id, highlightedPackage.Version),
                selectedPackages: mySelectedPackages,
                selectedIndex: null,
                recommendedCount: null,
                recommendPackages: null,
                recommenderVersion: null,
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

            // Vulnerabilities
            Assert.NotNull(lastTelemetryEvent.ComplexData["TopLevelVulnerablePackagesMaxSeverities"] as List<int>);
            var pkgSeverities = lastTelemetryEvent.ComplexData["TopLevelVulnerablePackagesMaxSeverities"] as List<int>;
            Assert.Equal(lastTelemetryEvent["TopLevelVulnerablePackagesCount"], pkgSeverities.Count());
            Assert.Collection(pkgSeverities,
                item => Assert.Equal(1, item),
                item => Assert.Equal(1, item),
                item => Assert.Equal(3, item));
            Assert.Equal(3, pkgSeverities.Count());
            Assert.NotNull(lastTelemetryEvent.ComplexData["TopLevelVulnerablePackages"]);

            // Action
            Assert.Null(lastTelemetryEvent["AddedPackages"]);
            Assert.Null(lastTelemetryEvent["RemovedPackages"]);
            Assert.Null(lastTelemetryEvent["ExistingPackages"]);
            Assert.Null(lastTelemetryEvent["TargetFrameworks"]);
            Assert.Null(lastTelemetryEvent["UpdatedPackagesOld"]);
            Assert.Null(lastTelemetryEvent["UpdatedPackagesNew"]);

            // Deprecation
            Assert.Null(lastTelemetryEvent["TopLevelDeprecatedPackages"]);

            // User cancellation
            Assert.Null(lastTelemetryEvent["AcceptedLicense"]);
            Assert.Null(lastTelemetryEvent["CancelAfterPreview"]);

            // Recommender
            Assert.NotNull(lastTelemetryEvent.ComplexData["SelectedPackage"]);
            Assert.Null(lastTelemetryEvent["SelectedIndex"]);
            Assert.Null(lastTelemetryEvent["RecommendedCount"]);
            Assert.Null(lastTelemetryEvent["RecommendPackages"]);
            Assert.Null(lastTelemetryEvent["Recommender.ModelVersion"]);
            Assert.Null(lastTelemetryEvent["Recommender.VsixVersion"]);
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
