﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Moq;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Test;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.PackageManagement.Test
{
    public class InstallationCompatibilityTests
    {
        [Fact]
        public void InstallationCompatibility_WithLowerMinClientVersion_Fails()
        {
            // Arrange
            var tc = new TestContext();
            tc.MinClientVersion = new NuGetVersion("10.0.0");
            var result = new DownloadResourceResult(Stream.Null, tc.PackageReader.Object);

            // Act & Assert
            var ex = Assert.Throws<MinClientVersionException>(() =>
                 tc.Target.EnsurePackageCompatibility(
                    tc.NuGetProject,
                    tc.PackageIdentityA,
                    result));

            Assert.Equal(
                "The 'PackageA 1.0.0' package requires NuGet client version '10.0.0' or above, " +
                $"but the current NuGet version is '{MinClientVersionUtility.GetNuGetClientVersion()}'. " +
                "To upgrade NuGet, please go to http://docs.nuget.org/consume/installing-nuget",
                ex.Message);

            tc.PackageReader.Verify(x => x.GetMinClientVersion(), Times.Never);
            tc.PackageReader.Verify(x => x.GetPackageTypes(), Times.Never);
            tc.NuspecReader.Verify(x => x.GetMinClientVersion(), Times.AtLeastOnce);
            tc.NuspecReader.Verify(x => x.GetPackageTypes(), Times.Never);
        }

        [Fact]
        public async Task InstallationCompatibility_WithValidProjectActions_Succeeds()
        {
            // Arrange
            using (var userPackageFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var tc = new TestContext(userPackageFolder);

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    userPackageFolder,
                    PackageSaveMode.Defaultv3,
                    new[]
                    {
                        new SimpleTestPackageContext(tc.PackageIdentityA)
                        {
                            PackageTypes = { PackageType.DotnetCliTool }
                        },
                        new SimpleTestPackageContext(tc.PackageIdentityB) // Not inspected, because this package is
                                                                          // being uninstalled, not installed.
                        {
                            PackageTypes = { tc.InvalidPackageType }
                        },
                        new SimpleTestPackageContext(tc.PackageIdentityC)
                        {
                            PackageTypes = { PackageType.Dependency }
                        },
                    });

                // Act & Assert
                tc.Target.EnsurePackageCompatibility(
                    tc.ProjectKProject,
                    tc.NuGetPathContext.Object,
                    new NuGetProjectAction[]
                    {
                        NuGetProjectAction.CreateInstallProjectAction(tc.PackageIdentityA, tc.SourceRepository),
                        NuGetProjectAction.CreateUninstallProjectAction(tc.PackageIdentityB),
                        NuGetProjectAction.CreateInstallProjectAction(tc.PackageIdentityC, tc.SourceRepository)
                    },
                    tc.GetRestoreResult(new[]
                    {
                        tc.PackageIdentityA,
                        tc.PackageIdentityB,
                        tc.PackageIdentityC
                    }));
            }
        }

        [Fact]
        public async Task InstallationCompatibility_WithInvalidProjectActions_Fails()
        {
            // Arrange
            using (var userPackageFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var tc = new TestContext(userPackageFolder);

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    userPackageFolder,
                    PackageSaveMode.Defaultv3,
                    new[]
                    {
                        new SimpleTestPackageContext(tc.PackageIdentityA)
                        {
                            PackageTypes = { tc.InvalidPackageType }
                        }
                    });

                // Act & Assert
                var ex = Assert.Throws<PackagingException>(() =>
                    tc.Target.EnsurePackageCompatibility(
                        tc.ProjectKProject,
                        tc.NuGetPathContext.Object,
                        new NuGetProjectAction[]
                        {
                            NuGetProjectAction.CreateInstallProjectAction(tc.PackageIdentityA, tc.SourceRepository)
                        },
                        tc.GetRestoreResult(new[] { tc.PackageIdentityA })));

                Assert.Equal(
                    "Package 'PackageA 1.0.0' has a package type 'Invalid 1.2' that is not supported by project 'TestProjectKNuGetProject'.",
                    ex.Message);
            }
        }

        [Fact]
        public void InstallationCompatibility_WithNuGetProject_WithInvalidType_Fails()
        {
            // Arrange
            var tc = new TestContext();
            tc.PackageTypes.Add(tc.InvalidPackageType);

            // Act & Assert
            tc.VerifyFailure(
                tc.NuGetProject,
                "Package 'PackageA 1.0.0' has a package type 'Invalid 1.2' that is not supported by project 'TestNuGetProject'.");
        }

        [Fact]
        public void InstallationCompatibility_WithNuGetProject_WithInvalidTypeAndEmptyVersion_Fails()
        {
            // Arrange
            var tc = new TestContext();
            tc.PackageTypes.Add(new PackageType("Invalid", PackageType.EmptyVersion));

            // Act & Assert
            tc.VerifyFailure(
                tc.NuGetProject,
                "Package 'PackageA 1.0.0' has a package type 'Invalid' that is not supported by project 'TestNuGetProject'.");
        }

        [Fact]
        public void InstallationCompatibility_WithNuGetProject_WitMultipleTypes_Fails()
        {
            // Arrange
            var tc = new TestContext();
            tc.PackageTypes.Add(PackageType.Legacy);
            tc.PackageTypes.Add(PackageType.Dependency);

            // Act & Assert
            tc.VerifyFailure(
                tc.NuGetProject,
                "Package 'PackageA 1.0.0' has multiple package types, which is not supported.");
        }

        [Fact]
        public void InstallationCompatibility_WithNuGetProject_WithDotnetCliToolType_Fails()
        {
            // Arrange
            var tc = new TestContext();
            tc.PackageTypes.Add(PackageType.DotnetCliTool);

            // Act & Assert
            tc.VerifyFailure(
                tc.NuGetProject,
                "Package 'PackageA 1.0.0' has a package type 'DotnetCliTool' that is not supported by project 'TestNuGetProject'.");
        }

        [Fact]
        public void InstallationCompatibility_WithNuGetProject_WithLegacyType_Succeeds()
        {
            // Arrange
            var tc = new TestContext();
            tc.PackageTypes.Add(PackageType.Legacy);

            // Act & Assert
            tc.VerifySuccess(tc.NuGetProject);
        }

        [Fact]
        public void InstallationCompatibility_WithNuGetProject_WithDependency_Succeeds()
        {
            // Arrange
            var tc = new TestContext();
            tc.PackageTypes.Add(PackageType.Dependency);

            // Act & Assert
            tc.VerifySuccess(tc.NuGetProject);
        }

        [Fact]
        public void InstallationCompatibility_WithProjectKProject_WithInvalidType_Fails()
        {
            // Arrange
            var tc = new TestContext();
            tc.PackageTypes.Add(tc.InvalidPackageType);

            // Act & Assert
            tc.VerifyFailure(
                tc.ProjectKProject,
                "Package 'PackageA 1.0.0' has a package type 'Invalid 1.2' that is not supported by project 'TestProjectKNuGetProject'.");
        }

        [Fact]
        public void InstallationCompatibility_WithProjectKProject_WithMultipleTypes_Fails()
        {
            // Arrange
            var tc = new TestContext();
            tc.PackageTypes.Add(PackageType.Legacy);
            tc.PackageTypes.Add(PackageType.Dependency);

            // Act & Assert
            tc.VerifyFailure(
                tc.ProjectKProject,
                "Package 'PackageA 1.0.0' has multiple package types, which is not supported.");
        }

        [Fact]
        public void InstallationCompatibility_WithProjectKProject_WithDotnetCliToolType_Succeeds()
        {
            // Arrange
            var tc = new TestContext();
            tc.PackageTypes.Add(PackageType.DotnetCliTool);

            // Act & Assert
            tc.VerifySuccess(tc.ProjectKProject);
        }

        [Fact]
        public void InstallationCompatibility_WithProjectKProject_WithLegacyType_Succeeds()
        {
            // Arrange
            var tc = new TestContext();
            tc.PackageTypes.Add(PackageType.Legacy);

            // Act & Assert
            tc.VerifySuccess(tc.ProjectKProject);
        }

        [Fact]
        public void InstallationCompatibility_WithProjectKProject_WithDependency_Succeeds()
        {
            // Arrange
            var tc = new TestContext();
            tc.PackageTypes.Add(PackageType.Dependency);

            // Act & Assert
            tc.VerifySuccess(tc.ProjectKProject);
        }

        private class TestContext
        {
            public TestContext(string userPackageFolder = null)
            {
                // Dependencies
                SourceRepository = new SourceRepository(new PackageSource("http://example/index.json"), Enumerable.Empty <INuGetResourceProvider>());

                PackageIdentityA = new PackageIdentity("PackageA", NuGetVersion.Parse("1.0.0"));
                PackageIdentityB = new PackageIdentity("PackageB", NuGetVersion.Parse("1.0.0"));
                PackageIdentityC = new PackageIdentity("PackageC", NuGetVersion.Parse("1.0.0"));
                PackageTypes = new List<PackageType>();
                MinClientVersion = new NuGetVersion(2, 0, 0);

                InvalidPackageType = new PackageType("Invalid", new Version(1, 2));

                NuGetProject = new TestNuGetProject(new List<PackageReference>());
                ProjectKProject = new TestProjectKNuGetProject();

                NuGetPathContext = new Mock<INuGetPathContext>();

                NuspecReader = new Mock<NuspecReader>(new XDocument());

                PackageReader = new Mock<PackageReaderBase>(
                    new FrameworkNameProvider(new[] { DefaultFrameworkMappings.Instance },
                    new[] { DefaultPortableFrameworkMappings.Instance }))
                {
                    CallBase = true
                };

                // Setup
                NuGetPathContext
                    .Setup(x => x.UserPackageFolder)
                    .Returns(userPackageFolder);
                NuGetPathContext
                    .Setup(x => x.FallbackPackageFolders)
                    .Returns(new string[0]);

                NuspecReader
                    .Setup(p => p.GetIdentity())
                    .Returns(() => PackageIdentityA);
                NuspecReader
                    .Setup(p => p.GetMinClientVersion())
                    .Returns(() => MinClientVersion);
                NuspecReader
                    .Setup(p => p.GetPackageTypes())
                    .Returns(() => PackageTypes);

                PackageReader
                    .Setup(p => p.GetIdentity())
                    .Returns(() => PackageIdentityA);
                PackageReader
                    .Setup(p => p.GetMinClientVersion())
                    .Returns(() => MinClientVersion);
                PackageReader
                    .Setup(p => p.GetPackageTypes())
                    .Returns(() => PackageTypes);

                PackageReader
                    .Setup(p => p.NuspecReader)
                    .Returns(() => NuspecReader.Object);

                Target = new InstallationCompatibility();
            }

            public SourceRepository SourceRepository { get; }
            public PackageIdentity PackageIdentityA { get; }
            public PackageIdentity PackageIdentityB { get; }
            public PackageIdentity PackageIdentityC { get; }
            public List<PackageType> PackageTypes { get; }
            public NuGetVersion MinClientVersion { get; set; }
            public TestNuGetProject NuGetProject { get; }
            public TestProjectKNuGetProject ProjectKProject { get; }
            public Mock<INuGetPathContext> NuGetPathContext { get; }
            public Mock<PackageReaderBase> PackageReader { get; }
            public Mock<NuspecReader> NuspecReader { get; }
            public InstallationCompatibility Target { get; }
            public PackageType InvalidPackageType { get; }

            public void VerifyFailure(
                NuGetProject nugetProject,
                string expected)
            {
                // Arrange
                var result = new DownloadResourceResult(Stream.Null, PackageReader.Object);

                // Act & Assert
                var ex = Assert.Throws<PackagingException>(() =>
                     Target.EnsurePackageCompatibility(
                        nugetProject,
                        PackageIdentityA,
                        result));

                Assert.Equal(expected, ex.Message);
                PackageReader.Verify(x => x.GetMinClientVersion(), Times.Never);
                PackageReader.Verify(x => x.GetPackageTypes(), Times.Never);
                NuspecReader.Verify(x => x.GetMinClientVersion(), Times.Once);
                NuspecReader.Verify(x => x.GetPackageTypes(), Times.Once);
            }

            public void VerifySuccess(NuGetProject nugetProject)
            {
                // Arrange
                var result = new DownloadResourceResult(Stream.Null, PackageReader.Object);

                // Act & Assert
                Target.EnsurePackageCompatibility(
                    nugetProject,
                    PackageIdentityA,
                    result);

                PackageReader.Verify(x => x.GetMinClientVersion(), Times.Never);
                PackageReader.Verify(x => x.GetPackageTypes(), Times.Never);
                NuspecReader.Verify(x => x.GetMinClientVersion(), Times.Once);
                NuspecReader.Verify(x => x.GetPackageTypes(), Times.Once);
            }

            public RestoreResult GetRestoreResult(IEnumerable<PackageIdentity> identities)
            {
                var node = new GraphNode<RemoteResolveResult>(new LibraryRange("project", LibraryDependencyTarget.All))
                {
                    Item = new GraphItem<RemoteResolveResult>(
                        new LibraryIdentity(
                            "project",
                            new NuGetVersion("1.0.0"),
                            LibraryType.Project))
                    {
                        Data = new RemoteResolveResult
                        {
                            Match = new RemoteMatch
                            {
                                Provider = null
                            }
                        }
                    }
                };

                foreach (var identity in identities)
                {
                    var dependencyNode = new GraphNode<RemoteResolveResult>(
                        new LibraryRange(
                            identity.Id,
                            new VersionRange(identity.Version),
                            LibraryDependencyTarget.All))
                    {
                        Item = new GraphItem<RemoteResolveResult>(
                            new LibraryIdentity(
                                identity.Id,
                                identity.Version,
                                LibraryType.Package))
                        {
                            Data = new RemoteResolveResult
                            {
                                Match = new RemoteMatch
                                {
                                    Provider = null
                                }
                            }
                        }
                    };

                    dependencyNode.OuterNode = node;
                    node.InnerNodes.Add(dependencyNode);
                }

                var graph = RestoreTargetGraph.Create(
                    false,
                    new[] { node },
                    new RemoteWalkContext(),
                    NullLogger.Instance,
                    FrameworkConstants.CommonFrameworks.NetStandard10);

                return new RestoreResult(
                    true,
                    new[] { graph },
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);
            }
        }
    }
}
