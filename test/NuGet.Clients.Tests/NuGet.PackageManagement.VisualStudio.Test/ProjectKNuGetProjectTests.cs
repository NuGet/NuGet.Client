// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem.Interop;
using Moq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class ProjectKNuGetProjectTests
    {
        [Fact]
        public async Task ProjectKNuGetProject_WithVersionRange_GetInstalledPackagesAsync()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var tc = new TestContext(testDirectory);

                tc.PackageManager
                    .Setup(x => x.GetInstalledPackagesAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new[] { new NuGetPackageMoniker { Id = "foo", Version = "1.0.0-*" } });

                // Act
                var actual = await tc.Target.GetInstalledPackagesAsync(CancellationToken.None);

                // Assert
                Assert.Equal(1, actual.Count());
                var packageReference = actual.First();
                Assert.Equal("foo", packageReference.PackageIdentity.Id);
                Assert.Equal(NuGetVersion.Parse("1.0.0--"), packageReference.PackageIdentity.Version);
            }
        }

        [Fact]
        public async Task ProjectKNuGetProject_WithPackageTypes_InstallPackageAsync()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var tc = new TestContext(testDirectory);
                using (var download = tc.InitializePackage())
                {
                    // Act
                    var result = await tc.Target.InstallPackageAsync(
                        tc.PackageIdentity,
                        download,
                        tc.ProjectContext.Object,
                        CancellationToken.None);

                    // Assert
                    Assert.True(result);
                    tc.PackageManager.Verify(
                        x => x.InstallPackageAsync(
                            It.Is<INuGetPackageMoniker>(y =>
                                y.Id == tc.PackageIdentity.Id &&
                                y.Version == tc.PackageIdentity.Version.ToNormalizedString()),
                            It.Is<IReadOnlyDictionary<string, object>>(y =>
                                y.ContainsKey("PackageTypes") &&
                                Enumerable.SequenceEqual((IEnumerable<PackageType>)y["PackageTypes"], tc.PackageTypes)),
                            It.IsAny<TextWriter>(),
                            It.IsAny<IProgress<INuGetPackageInstallProgress>>(),
                            It.IsAny<CancellationToken>()),
                        Times.Once);
                }
            }
        }

        [Fact]
        public async Task ProjectKNuGetProject_WithFrameworks_InstallPackageAsync()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var tc = new TestContext(testDirectory);
                using (var download = tc.InitializePackage())
                {
                    // Act
                    var result = await tc.Target.InstallPackageAsync(
                        tc.PackageIdentity,
                        download,
                        tc.ProjectContext.Object,
                        CancellationToken.None);

                    // Assert
                    Assert.True(result);
                    tc.PackageManager.Verify(
                        x => x.InstallPackageAsync(
                            It.Is<INuGetPackageMoniker>(y =>
                                y.Id == tc.PackageIdentity.Id &&
                                y.Version == tc.PackageIdentity.Version.ToNormalizedString()),
                            It.Is<IReadOnlyDictionary<string, object>>(y =>
                                y.ContainsKey("Frameworks") &&
                                Enumerable.SequenceEqual(
                                    (IEnumerable<NuGetFramework>)y["Frameworks"],
                                    new[]
                                    {
                                        NuGetFramework.Parse("net45"),
                                        NuGetFramework.Parse("netstandard1.0")
                                    })),
                            It.IsAny<TextWriter>(),
                            It.IsAny<IProgress<INuGetPackageInstallProgress>>(),
                            It.IsAny<CancellationToken>()),
                        Times.Once);
                }
            }
        }

        private class TestContext
        {
            public TestContext(TestDirectory testDirectory)
            {
                // Dependencies
                TestDirectory = testDirectory;
                PackageManager = new Mock<INuGetPackageManager>();
                ProjectContext = new Mock<INuGetProjectContext>();
                var projectName = "ProjectName";
                var uniqueName = "UniqueName";
                var projectId = string.Empty;
                PackageIdentity = new PackageIdentity("PackageA", new NuGetVersion("1.0.0-beta"));
                PackageTypes = new List<PackageType>
                {
                    new PackageType("Foo", Version.Parse("1.0.0")),
                    new PackageType("Bar", Version.Parse("2.0"))
                };
                SupportedFrameworks = new List<NuGetFramework>
                {
                    new NuGetFramework("net40"),
                    new NuGetFramework("netcoreapp1.0")
                };

                // Setup
                PackageManager
                    .Setup(x => x.GetSupportedFrameworksAsync(It.IsAny<CancellationToken>()))
                    .Returns(() => Task.FromResult<IReadOnlyCollection<FrameworkName>>(
                        SupportedFrameworks.Select(f => new FrameworkName(f.DotNetFrameworkName)).ToList()));

                PackageManager
                    .Setup(x => x.GetInstalledPackagesAsync(It.IsAny<CancellationToken>()))
                    .Returns(() => Task.FromResult<IReadOnlyCollection<object>>(new object[0]));
                
                Target = new ProjectKNuGetProject(
                    PackageManager.Object,
                    projectName,
                    uniqueName,
                    projectId);
            }

            public TestDirectory TestDirectory { get; }
            public Mock<INuGetPackageManager> PackageManager { get; }
            public Mock<INuGetProjectContext> ProjectContext { get; }
            public PackageIdentity PackageIdentity { get; }
            public ProjectKNuGetProject Target { get; }
            public List<PackageType> PackageTypes { get; }
            public List<NuGetFramework> SupportedFrameworks { get; }

            public DownloadResourceResult InitializePackage()
            {
                var context = new SimpleTestPackageContext(PackageIdentity);
                context.PackageTypes.Clear();
                context.PackageTypes.AddRange(PackageTypes);

                var package = SimpleTestPackageUtility.CreateFullPackage(
                    TestDirectory,
                    context);

                return new DownloadResourceResult(package.OpenRead());
            }
        }
    }
}
