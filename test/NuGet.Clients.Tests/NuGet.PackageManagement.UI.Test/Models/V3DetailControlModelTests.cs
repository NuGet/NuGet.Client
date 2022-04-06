// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.ServiceBroker;
using Moq;
using NuGet.Frameworks;
using NuGet.PackageManagement.UI.Utility;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using NuGet.Versioning;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.UI.Test.Models
{
    [Collection(MockedVS.Collection)]
    public abstract class V3DetailControlModelTestBase : IClassFixture<V3PackageSearchMetadataFixture>
    {
        protected readonly V3PackageSearchMetadataFixture _testData;
        protected readonly PackageItemViewModel _testViewModel;

        public V3DetailControlModelTestBase(GlobalServiceProvider sp, V3PackageSearchMetadataFixture testData)
        {
            sp.Reset();
            _testData = testData;

            // The versions pre-baked into the view model provide data for the first step of metadata extraction
            // which fails (null) in a V3 scenario--they need to be extracted using a metadata provider (below)
            var testVersion = new NuGetVersion(0, 0, 1);
            var testVersions = new List<VersionInfoContextInfo>() {
                new VersionInfoContextInfo(new NuGetVersion(0, 0, 1)),
                new VersionInfoContextInfo(new NuGetVersion(0, 0, 2))
            };

            var searchService = new Mock<IReconnectingNuGetSearchService>();
            _testViewModel = new PackageItemViewModel(searchService.Object)
            {
                Id = "nuget.psm",
                Version = testVersion,
                InstalledVersion = testVersion,
                Sources = new List<PackageSourceContextInfo> { new PackageSourceContextInfo("nuget.psm.test") },
            };
        }

        /// <summary>
        /// Due to embedding the types we need to compare based on IsEquivalentTo
        /// </summary>
        protected class TypeEquivalenceComparer : IEqualityComparer<Type>
        {
            public static readonly TypeEquivalenceComparer Instance = new TypeEquivalenceComparer();

            private TypeEquivalenceComparer()
            {
            }

            public bool Equals(Type x, Type y)
            {
                return x.IsEquivalentTo(y);
            }

            public int GetHashCode(Type obj)
            {
                return obj.GUID.GetHashCode();
            }
        }
    }

    public class V3PackageDetailControlModelTests : V3DetailControlModelTestBase, IAsyncServiceProvider
    {
        private readonly Dictionary<Type, Task<object>> _services = new Dictionary<Type, Task<object>>(TypeEquivalenceComparer.Instance);
        private readonly PackageDetailControlModel _testInstance;
        public V3PackageDetailControlModelTests(GlobalServiceProvider sp, V3PackageSearchMetadataFixture testData)
            : base(sp, testData)
        {
            var solMgr = new Mock<INuGetSolutionManagerService>();

            var packageSearchMetadata = new List<PackageSearchMetadataContextInfo>()
            {
                PackageSearchMetadataContextInfo.Create(_testData.TestData)
            };

            var mockSearchService = new Mock<INuGetSearchService>();
            mockSearchService.Setup(x =>
                x.GetPackageMetadataListAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyCollection<PackageSourceContextInfo>>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<IReadOnlyCollection<PackageSearchMetadataContextInfo>>(packageSearchMetadata));

            mockSearchService.Setup(x =>
                x.GetDeprecationMetadataAsync(
                    It.IsAny<PackageIdentity>(),
                    It.IsAny<IReadOnlyCollection<PackageSourceContextInfo>>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Returns(null);

            mockSearchService.Setup(x => x.GetPackageMetadataAsync(
                    It.IsAny<PackageIdentity>(),
                    It.IsAny<IReadOnlyCollection<PackageSourceContextInfo>>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<(PackageSearchMetadataContextInfo, PackageDeprecationMetadataContextInfo)>((packageSearchMetadata[0], null)));

            var mockServiceBroker = new Mock<IServiceBroker>();
#pragma warning disable ISB001 // Dispose of proxies
            mockServiceBroker.Setup(
                x => x.GetProxyAsync<INuGetSearchService>(
                    NuGetServices.SearchService,
                    It.IsAny<ServiceActivationOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<INuGetSearchService>(mockSearchService.Object));
#pragma warning restore ISB001 // Dispose of proxies

            _testInstance = new PackageDetailControlModel(
                mockServiceBroker.Object,
                solutionManager: solMgr.Object,
                Array.Empty<IProjectContextInfo>());
            _testInstance.SetCurrentPackageAsync(
                _testViewModel,
                ItemFilter.All,
                () => null).Wait();
        }

        [Fact]
        public void ViewModelMarkedVulnerableWhenMetadataHasVulnerability_Flagged()
        {
            Assert.True(_testInstance.IsPackageVulnerable);
        }

        [Fact]
        public void MaxVulnerabilitySeverityWhenMetadataHasVulnerability_Calculated()
        {
            Assert.Equal(_testInstance.PackageVulnerabilityMaxSeverity, _testData.TestData.Vulnerabilities.Max(v => v.Severity));
        }

        [Fact]
        public void VulnerabilityCountWhenMetadataHasVulnerability_Calculated()
        {
            Assert.Equal(_testInstance.PackageVulnerabilityCount, _testData.TestData.Vulnerabilities.Count());
        }

        [Fact]
        public void PackageVulnerabilities_WhenMetadataHasVulnerability_IsOrderedBySeverityDescending()
        {
            IEnumerable<PackageVulnerabilityMetadataContextInfo> sortedTestVulnerabilities =
                _testData.TestData.Vulnerabilities
                .OrderByDescending(v => v.Severity)
                .Select(v => new PackageVulnerabilityMetadataContextInfo(v.AdvisoryUrl, v.Severity));

            Assert.Equal(sortedTestVulnerabilities, _testInstance.PackageVulnerabilities);
        }

        [Fact]
        public async Task SetCurrentPackageAsync_SortsVersions_ByNuGetVersionDesc()
        {
            // Arrange
            NuGetVersion installedVersion = NuGetVersion.Parse("1.0.0");

            var testVersions = new List<VersionInfoContextInfo>() {
                new VersionInfoContextInfo(new NuGetVersion("2.10.1-dev-01248")),
                new VersionInfoContextInfo(new NuGetVersion("2.10.1-dev-01249")),
                new VersionInfoContextInfo(new NuGetVersion("2.10.1-dev-01256")),
                new VersionInfoContextInfo(new NuGetVersion("2.10.1-dev-01265")),
                new VersionInfoContextInfo(new NuGetVersion("2.10.0-dev-01187")),
                new VersionInfoContextInfo(new NuGetVersion("2.10.0-dev-01191")),
                new VersionInfoContextInfo(new NuGetVersion("2.10.0-dev-01211")),
                new VersionInfoContextInfo(new NuGetVersion("2.10.0")),
            };

            var searchService = new Mock<IReconnectingNuGetSearchService>();
            searchService.Setup(ss => ss.GetPackageVersionsAsync(It.IsAny<PackageIdentity>(), It.IsAny<IReadOnlyCollection<PackageSourceContextInfo>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testVersions);

            var vm = new PackageItemViewModel(searchService.Object)
            {
                Id = "package",
                InstalledVersion = installedVersion,
                Version = installedVersion,
            };

            // Act

            await _testInstance.SetCurrentPackageAsync(
                vm,
                ItemFilter.All,
                () => vm);

            // Assert
            var expectedAdditionalInfo = string.Empty;

            // Remove any added `null` separators, and any Additional Info entries (eg, "Latest Prerelease", "Latest Stable").
            List<DisplayVersion> actualVersions = _testInstance.Versions
                .Where(v => v != null && v.AdditionalInfo == expectedAdditionalInfo).ToList();

            var expectedVersions = new List<DisplayVersion>() {
                new DisplayVersion(version: new NuGetVersion("2.10.1-dev-01265"), additionalInfo: expectedAdditionalInfo),
                new DisplayVersion(version: new NuGetVersion("2.10.1-dev-01256"), additionalInfo: expectedAdditionalInfo),
                new DisplayVersion(version: new NuGetVersion("2.10.1-dev-01249"), additionalInfo: expectedAdditionalInfo),
                new DisplayVersion(version: new NuGetVersion("2.10.1-dev-01248"), additionalInfo: expectedAdditionalInfo),
                new DisplayVersion(version: new NuGetVersion("2.10.0"), additionalInfo: expectedAdditionalInfo),
                new DisplayVersion(version: new NuGetVersion("2.10.0-dev-01211"), additionalInfo: expectedAdditionalInfo),
                new DisplayVersion(version: new NuGetVersion("2.10.0-dev-01191"), additionalInfo: expectedAdditionalInfo),
                new DisplayVersion(version: new NuGetVersion("2.10.0-dev-01187"), additionalInfo: expectedAdditionalInfo),
            };

            Assert.Equal(expectedVersions, actualVersions);
        }

        [Fact]
        public async Task SetCurrentPackageAsync_ClearVersions_Always()
        {
            // Arrange
            var installedVersion = NuGetVersion.Parse("1.0.0");

            var testVersions = new List<VersionInfoContextInfo>() {
                new VersionInfoContextInfo(new NuGetVersion("1.0.0")),
                new VersionInfoContextInfo(new NuGetVersion("1.0.1")),
            };

            var mockPropertyChangedEventHandler = new Mock<IPropertyChangedEventHandler>();
            var wasVersionsListCleared = false;

            var searchService = new Mock<IReconnectingNuGetSearchService>();
            searchService.Setup(s => s.GetPackageVersionsAsync(It.IsAny<PackageIdentity>(), It.IsAny<IReadOnlyCollection<PackageSourceContextInfo>>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testVersions);
            var vm = new PackageItemViewModel(searchService.Object);

            vm.Id = "a";
            vm.Sources = new ReadOnlyCollection<PackageSourceContextInfo>(new List<PackageSourceContextInfo>());
            vm.InstalledVersion = installedVersion;
            vm.Version = installedVersion;

            // Test Setup already selected a package.
            int previousVersionListCount = _testInstance.Versions.Count;

            mockPropertyChangedEventHandler.Setup(x => x.PropertyChanged(
                It.IsAny<object>(),
                It.IsAny<PropertyChangedEventArgs>()
            ))
            .Callback<object, PropertyChangedEventArgs>((d, p) =>
            {
                DetailControlModel detail = d as DetailControlModel;
                if (detail != null
                    && detail.Versions.Count == 0
                    && p.PropertyName == nameof(DetailControlModel.Versions))
                {
                    wasVersionsListCleared = true;
                }
            });

            _testInstance.PropertyChanged += mockPropertyChangedEventHandler.Object.PropertyChanged;

            // Act

            //Select a different VM which should clear the Versions list from the previous selection.
            await _testInstance.SetCurrentPackageAsync(
                vm,
                ItemFilter.All,
                () => vm);

            // Assert

            Assert.True(previousVersionListCount > 0, "Test setup did not pre-populate versions list.");
            Assert.True(wasVersionsListCleared, "Versions list was not cleared.");
        }

        [Theory]
        [InlineData("*", "2.10.0")]
        [InlineData("1.*", "2.10.0")]
        [InlineData("(0.1,3.4)", "2.10.0")]
        [InlineData("2.10.0", "2.10.0")]
        public async void IsSelectedVersionCorrect_WhenPackageStyleIsPackageReference_And_CustomVersion(string allowedVersions, string installedVersion)
        {
            // Arange project
            var mockServiceBroker = new Mock<IServiceBroker>();
            var mockSearchService = new Mock<INuGetSearchService>();

            var packageIdentity = new PackageIdentity("Contoso.A", NuGetVersion.Parse(installedVersion));

            var installedPackages = new PackageReferenceContextInfo[]
            {
                PackageReferenceContextInfo.Create(
                    new PackageReference(
                        packageIdentity,
                        NuGetFramework.Parse("net45"),
                        userInstalled: true,
                        developmentDependency: false,
                        requireReinstallation: false,
                        allowedVersions: VersionRange.Parse(allowedVersions)))
            };

            var projectManagerService = new Mock<INuGetProjectManagerService>();
            projectManagerService.Setup(x => x.GetInstalledPackagesAsync(
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<IReadOnlyCollection<IPackageReferenceContextInfo>>(installedPackages));

#pragma warning disable ISB001 // Dispose of proxies
            mockServiceBroker.Setup(x => x.GetProxyAsync<INuGetProjectManagerService>(It.Is<ServiceJsonRpcDescriptor>(d => d.Moniker == NuGetServices.ProjectManagerService.Moniker), It.IsAny<ServiceActivationOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(projectManagerService.Object);
#pragma warning restore ISB001 // Dispose of proxies

            var project = new Mock<IProjectContextInfo>();

            project.SetupGet(p => p.ProjectKind).Returns(NuGetProjectKind.PackageReference);
            project.SetupGet(p => p.ProjectStyle).Returns(ProjectModel.ProjectStyle.PackageReference);
            project.SetupGet(p => p.ProjectId).Returns("ProjectId");

            var model = new PackageDetailControlModel(
                mockServiceBroker.Object,
                solutionManager: new Mock<INuGetSolutionManagerService>().Object,
                projects: new[] { project.Object });

            // Arrange
            var testVersions = new List<VersionInfoContextInfo>() {
                new VersionInfoContextInfo(new NuGetVersion("2.10.1-dev-01248")),
                new VersionInfoContextInfo(new NuGetVersion("2.10.0")),
            };

            var searchService = new Mock<IReconnectingNuGetSearchService>();
            searchService.Setup(ss => ss.GetPackageVersionsAsync(It.IsAny<PackageIdentity>(), It.IsAny<IReadOnlyCollection<PackageSourceContextInfo>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testVersions);

            // Act
            var vm = new PackageItemViewModel(searchService.Object)
            {
                Id = "Contoso.A",
                Sources = new List<PackageSourceContextInfo> { new PackageSourceContextInfo("Contoso.A.test") },
                InstalledVersion = NuGetVersion.Parse(installedVersion),
                AllowedVersions = VersionRange.Parse(allowedVersions),
                Version = NuGetVersion.Parse(installedVersion),
            };

            await model.SetCurrentPackageAsync(
                vm,
                ItemFilter.Installed,
                () => vm);

            // Assert
            VersionRange installedVersionRange = VersionRange.Parse(allowedVersions, true);
            NuGetVersion bestVersion = installedVersionRange.FindBestMatch(testVersions.Select(t => t.Version));
            var displayVersion = new DisplayVersion(installedVersionRange, bestVersion, additionalInfo: null);

            Assert.Equal(model.SelectedVersion.ToString(), allowedVersions);
            Assert.Equal(model.Versions.FirstOrDefault(), displayVersion);
        }

        [Theory]
        [InlineData(NuGetProjectKind.PackagesConfig, ProjectModel.ProjectStyle.PackagesConfig, null, "2.10.0")]
        [InlineData(NuGetProjectKind.Unknown, ProjectModel.ProjectStyle.Unknown, "(0.1,3.4)", "2.10.0")]
        [InlineData(NuGetProjectKind.ProjectK, ProjectModel.ProjectStyle.ProjectJson, "(0.1,3.4)", "2.10.0")]
        [InlineData(NuGetProjectKind.Unknown, ProjectModel.ProjectStyle.DotnetCliTool, "*", "2.10.0")]
        public async void IsSelectedVersionCorrect_WhenPackageStyleIsNotPackageReference_And_CustomVersion(NuGetProjectKind projectKind, ProjectModel.ProjectStyle projectStyle, string allowedVersions, string installedVersion)
        {
            // Arange project
            var mockServiceBroker = new Mock<IServiceBroker>();
            var mockSearchService = new Mock<INuGetSearchService>();

            var packageIdentity = new PackageIdentity("Contoso.A", NuGetVersion.Parse(installedVersion));

            var installedPackages = new PackageReferenceContextInfo[]
            {
                PackageReferenceContextInfo.Create(
                    new PackageReference(
                        packageIdentity,
                        NuGetFramework.Parse("net45"),
                        userInstalled: true,
                        developmentDependency: false,
                        requireReinstallation: false,
                        allowedVersions: allowedVersions != null ? VersionRange.Parse(allowedVersions) : null))
            };

            var projectManagerService = new Mock<INuGetProjectManagerService>();
            projectManagerService.Setup(x => x.GetInstalledPackagesAsync(
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<IReadOnlyCollection<IPackageReferenceContextInfo>>(installedPackages));

#pragma warning disable ISB001 // Dispose of proxies
            mockServiceBroker.Setup(x => x.GetProxyAsync<INuGetProjectManagerService>(It.Is<ServiceJsonRpcDescriptor>(d => d.Moniker == NuGetServices.ProjectManagerService.Moniker), It.IsAny<ServiceActivationOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(projectManagerService.Object);
#pragma warning restore ISB001 // Dispose of proxies

            var project = new Mock<IProjectContextInfo>();

            project.SetupGet(p => p.ProjectKind).Returns(projectKind);
            project.SetupGet(p => p.ProjectStyle).Returns(projectStyle);
            project.SetupGet(p => p.ProjectId).Returns("ProjectId");

            var model = new PackageDetailControlModel(
                mockServiceBroker.Object,
                solutionManager: new Mock<INuGetSolutionManagerService>().Object,
                projects: new[] { project.Object });

            // Arrange
            var testVersions = new List<VersionInfoContextInfo>() {
                new VersionInfoContextInfo(new NuGetVersion("2.10.1-dev-01248")),
                new VersionInfoContextInfo(new NuGetVersion("2.10.0")),
            };

            var searchService = new Mock<IReconnectingNuGetSearchService>();
            searchService.Setup(ss => ss.GetPackageVersionsAsync(It.IsAny<PackageIdentity>(), It.IsAny<IReadOnlyCollection<PackageSourceContextInfo>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testVersions);

            // Act
            var vm = new PackageItemViewModel(searchService.Object)
            {
                Id = "Contoso.A",
                Sources = new List<PackageSourceContextInfo> { new PackageSourceContextInfo("Contoso.A.test") },
                InstalledVersion = NuGetVersion.Parse(installedVersion),
                AllowedVersions = allowedVersions != null ? VersionRange.Parse(allowedVersions) : null,
                Version = NuGetVersion.Parse(installedVersion),
            };

            await model.SetCurrentPackageAsync(
                vm,
                ItemFilter.All,
                () => vm);

            // Assert
            Assert.NotEqual(model.SelectedVersion.ToString(), allowedVersions);
            Assert.Equal(model.SelectedVersion.Version.ToString(), installedVersion);
        }

        public Task<object> GetServiceAsync(Type serviceType)
        {
            if (_services.TryGetValue(serviceType, out Task<object> task))
            {
                return task;
            }

            return Task.FromResult<object>(null);
        }

        /// <summary>
        /// We need an interface that implements both: SVsBrokeredServiceContainer and IBrokeredServiceContainer so we can add it to the service host
        /// </summary>
        public interface IBrokeredServiceContainerMock : SVsBrokeredServiceContainer, IBrokeredServiceContainer
        {
        }
    }

    public class V3PackageSolutionDetailControlModelTests : V3DetailControlModelTestBase, IAsyncServiceProvider
    {
        private PackageSolutionDetailControlModel _testInstance;
        private readonly Dictionary<Type, Task<object>> _services = new Dictionary<Type, Task<object>>(TypeEquivalenceComparer.Instance);
        public V3PackageSolutionDetailControlModelTests(GlobalServiceProvider sp, V3PackageSearchMetadataFixture testData)
            : base(sp, testData)
        {
            var packageSearchMetadata = new List<PackageSearchMetadataContextInfo>()
            {
                PackageSearchMetadataContextInfo.Create(_testData.TestData)
            };

            var mockSearchService = new Mock<INuGetSearchService>();
            mockSearchService.Setup(x =>
                x.GetPackageMetadataListAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyCollection<PackageSourceContextInfo>>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<IReadOnlyCollection<PackageSearchMetadataContextInfo>>(packageSearchMetadata));

            mockSearchService.Setup(x =>
                x.GetDeprecationMetadataAsync(
                    It.IsAny<PackageIdentity>(),
                    It.IsAny<IReadOnlyCollection<PackageSourceContextInfo>>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Returns(null);

            mockSearchService.Setup(x => x.GetPackageMetadataAsync(
                    It.IsAny<PackageIdentity>(),
                    It.IsAny<IReadOnlyCollection<PackageSourceContextInfo>>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<(PackageSearchMetadataContextInfo, PackageDeprecationMetadataContextInfo)>((packageSearchMetadata[0], null)));

            var solMgr = new Mock<INuGetSolutionManagerService>();
            var serviceBroker = new Mock<IServiceBroker>();
            var projectManagerService = new Mock<INuGetProjectManagerService>();
            projectManagerService.Setup(x => x.GetProjectsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<IProjectContextInfo>());

#pragma warning disable ISB001 // Dispose of proxies
            serviceBroker.Setup(x => x.GetProxyAsync<INuGetProjectManagerService>(It.Is<ServiceJsonRpcDescriptor>(d => d.Moniker == NuGetServices.ProjectManagerService.Moniker), It.IsAny<ServiceActivationOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(projectManagerService.Object);
            serviceBroker.Setup(
                x => x.GetProxyAsync<INuGetSearchService>(
                    NuGetServices.SearchService,
                    It.IsAny<ServiceActivationOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<INuGetSearchService>(mockSearchService.Object));
#pragma warning restore ISB001 // Dispose of proxies

            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                _testInstance = await PackageSolutionDetailControlModel.CreateAsync(
                    solutionManager: solMgr.Object,
                    projects: new List<IProjectContextInfo>(),
                    serviceBroker: serviceBroker.Object,
                    CancellationToken.None);

                await _testInstance.SetCurrentPackageAsync(
                    _testViewModel,
                    ItemFilter.All,
                    () => null);
            });
        }

        public Task<object> GetServiceAsync(Type serviceType)
        {
            if (_services.TryGetValue(serviceType, out Task<object> task))
            {
                return task;
            }

            return Task.FromResult<object>(null);
        }

        [Fact]
        public async Task SetCurrentPackageAsync_SortsVersions_ByNuGetVersionDesc()
        {
            // Arrange
            NuGetVersion installedVersion = NuGetVersion.Parse("1.0.0");

            var testVersions = new List<VersionInfoContextInfo>() {
                new VersionInfoContextInfo(new NuGetVersion("2.10.1-dev-01248")),
                new VersionInfoContextInfo(new NuGetVersion("2.10.1-dev-01249")),
                new VersionInfoContextInfo(new NuGetVersion("2.10.1-dev-01256")),
                new VersionInfoContextInfo(new NuGetVersion("2.10.1-dev-01265")),
                new VersionInfoContextInfo(new NuGetVersion("2.10.0-dev-01187")),
                new VersionInfoContextInfo(new NuGetVersion("2.10.0-dev-01191")),
                new VersionInfoContextInfo(new NuGetVersion("2.10.0-dev-01211")),
                new VersionInfoContextInfo(new NuGetVersion("2.10.0")),
            };

            var searchService = new Mock<IReconnectingNuGetSearchService>();
            searchService.Setup(ss => ss.GetPackageVersionsAsync(It.IsAny<PackageIdentity>(), It.IsAny<IReadOnlyCollection<PackageSourceContextInfo>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testVersions);

            var vm = new PackageItemViewModel(searchService.Object)
            {
                Id = "package",
                InstalledVersion = installedVersion,
                Version = installedVersion,
            };

            // Act

            await _testInstance.SetCurrentPackageAsync(
                vm,
                ItemFilter.All,
                () => vm);

            // Assert
            var expectedAdditionalInfo = string.Empty;

            // Remove any added `null` separators, and any Additional Info entries (eg, "Latest Prerelease", "Latest Stable").
            List<DisplayVersion> actualVersions = _testInstance.Versions
                .Where(v => v != null && v.AdditionalInfo == expectedAdditionalInfo).ToList();

            var expectedVersions = new List<DisplayVersion>() {
                new DisplayVersion(version: new NuGetVersion("2.10.1-dev-01265"), additionalInfo: expectedAdditionalInfo),
                new DisplayVersion(version: new NuGetVersion("2.10.1-dev-01256"), additionalInfo: expectedAdditionalInfo),
                new DisplayVersion(version: new NuGetVersion("2.10.1-dev-01249"), additionalInfo: expectedAdditionalInfo),
                new DisplayVersion(version: new NuGetVersion("2.10.1-dev-01248"), additionalInfo: expectedAdditionalInfo),
                new DisplayVersion(version: new NuGetVersion("2.10.0"), additionalInfo: expectedAdditionalInfo),
                new DisplayVersion(version: new NuGetVersion("2.10.0-dev-01211"), additionalInfo: expectedAdditionalInfo),
                new DisplayVersion(version: new NuGetVersion("2.10.0-dev-01191"), additionalInfo: expectedAdditionalInfo),
                new DisplayVersion(version: new NuGetVersion("2.10.0-dev-01187"), additionalInfo: expectedAdditionalInfo),
            };

            Assert.Equal(expectedVersions, actualVersions);
        }

        [Fact]
        public async Task SetCurrentPackageAsync_ClearVersions_Always()
        {
            // Arrange
            var installedVersion = NuGetVersion.Parse("1.0.0");

            var testVersions = new List<VersionInfoContextInfo>() {
                new VersionInfoContextInfo(new NuGetVersion("1.0.0")),
                new VersionInfoContextInfo(new NuGetVersion("1.0.1")),
            };

            var mockPropertyChangedEventHandler = new Mock<IPropertyChangedEventHandler>();
            var wasVersionsListCleared = false;

            var searchService = new Mock<IReconnectingNuGetSearchService>();
            searchService.Setup(s => s.GetPackageVersionsAsync(It.IsAny<PackageIdentity>(), It.IsAny<IReadOnlyCollection<PackageSourceContextInfo>>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testVersions);
            var vm = new PackageItemViewModel(searchService.Object);

            vm.Id = "a";
            vm.Sources = new ReadOnlyCollection<PackageSourceContextInfo>(new List<PackageSourceContextInfo>());
            vm.InstalledVersion = installedVersion;
            vm.Version = installedVersion;

            // Test Setup already selected a package.
            int previousVersionListCount = _testInstance.Versions.Count;

            mockPropertyChangedEventHandler.Setup(x => x.PropertyChanged(
                It.IsAny<object>(),
                It.IsAny<PropertyChangedEventArgs>()
            ))
            .Callback<object, PropertyChangedEventArgs>((d, p) =>
            {
                DetailControlModel detail = d as DetailControlModel;
                if (detail != null
                    && detail.Versions.Count == 0
                    && p.PropertyName == nameof(DetailControlModel.Versions))
                {
                    wasVersionsListCleared = true;
                }
            });

            _testInstance.PropertyChanged += mockPropertyChangedEventHandler.Object.PropertyChanged;

            // Act

            //Select a different VM which should clear the Versions list from the previous selection.
            await _testInstance.SetCurrentPackageAsync(
                vm,
                ItemFilter.All,
                () => vm);

            // Assert

            Assert.True(previousVersionListCount > 0, "Test setup did not pre-populate versions list.");
            Assert.True(wasVersionsListCleared, "Versions list was not cleared.");
        }
    }

    public interface IPropertyChangedEventHandler
    {
        void PropertyChanged(object sender, PropertyChangedEventArgs e);
    }
}
