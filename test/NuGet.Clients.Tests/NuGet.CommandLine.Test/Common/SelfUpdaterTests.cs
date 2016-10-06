// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Reflection;
using Moq;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class SelfUpdaterTests
    {
        [Theory]
        [InlineData("1.1.1", true, false)]
        [InlineData("1.1.1", false, false)]
        [InlineData("1.1.1-beta", true, false)]
        [InlineData("1.1.1-beta", false, false)]
        [InlineData("99.99.99", true, true)]
        [InlineData("99.99.99", false, true)]
        [InlineData("99.99.99-beta", true, true)]
        [InlineData("99.99.99-beta", false, false)]
        public void SelfUpdater_WithArbitraryVersions_UpdateSelf(string version, bool prerelease, bool replaced)
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var tc = new TestContext(testDirectory);
                tc
                    .Package
                    .Setup(x => x.Version)
                    .Returns(new SemanticVersion(version));

                // Act
                tc.Target.UpdateSelf(prerelease);

                // Assert
                tc.VerifyReplacedState(replaced);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SelfUpdater_WithCurrentVersion(bool prerelease)
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var tc = new TestContext(testDirectory);
                tc.Package.Setup(x => x.Version).Returns(tc.ClientVersion);

                // Act
                tc.Target.UpdateSelf(prerelease);

                // Assert
                tc.VerifyReplacedState(replaced: false);
            }
        }

        private class TestContext
        {
            public TestContext(TestDirectory directory)
            {
                Directory = directory;
                Factory = new Mock<IPackageRepositoryFactory>();
                Repository = new Mock<IPackageRepository>();
                Console = new Mock<IConsole>();
                Package = new Mock<IPackage>();
                PackageFile = new Mock<IPackageFile>();
                OriginalContent = new byte[] { 0 };
                NewContent = new byte[] { 1 };

                var clientVersion = typeof(SelfUpdater)
                    .Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    .InformationalVersion;
                ClientVersion = new SemanticVersion(clientVersion);
                IsClientPrerelease = NuGetVersion
                    .Parse(clientVersion)
                    .IsPrerelease;

                PackageFile.Setup(x => x.Path).Returns("nuget.exe");
                PackageFile.Setup(x => x.GetStream()).Returns(() => new MemoryStream(NewContent));

                Package.Setup(x => x.Id).Returns("NuGet.CommandLine");
                Package.Setup(x => x.Version).Returns(new SemanticVersion("99.99.99"));
                Package.Setup(x => x.Listed).Returns(true);
                Package
                    .Setup(x => x.GetFiles())
                    .Returns(() => PackageFile != null ? new[] { PackageFile.Object }.AsEnumerable() : null);

                var zero = new Mock<IPackage>();
                zero.Setup(x => x.Id).Returns("NuGet.CommandLine");
                zero.Setup(x => x.Version).Returns(new SemanticVersion("0.0.0"));
                zero.Setup(x => x.Listed).Returns(true);

                Target = new SelfUpdater(Factory.Object);
                Target.Console = Console.Object;
                Target.AssemblyLocation = Path.Combine(Directory, "nuget.exe");

                Factory
                    .Setup(x => x.CreateRepository(It.IsAny<string>()))
                    .Returns(Repository.Object);

                Repository
                    .Setup(x => x.GetPackages())
                    .Returns(() => Package != null ? new[] { zero.Object, Package.Object }.AsQueryable() : null);

                File.WriteAllBytes(Target.AssemblyLocation, OriginalContent);
            }

            public Mock<IPackageRepositoryFactory> Factory { get; }
            public Mock<IPackageRepository> Repository { get; }
            public Mock<IConsole> Console { get; }
            public SelfUpdater Target { get; }
            public Mock<IPackage> Package { get; set; }
            public TestDirectory Directory { get; }
            public Mock<IPackageFile> PackageFile { get; }
            public byte[] NewContent { get; set; }
            public byte[] OriginalContent { get; }
            public SemanticVersion ClientVersion { get; }
            public bool IsClientPrerelease { get; }

            public void VerifyReplacedState(bool replaced)
            {
                Assert.True(File.Exists(Target.AssemblyLocation), "nuget.exe should still exist.");
                var actualContent = File.ReadAllBytes(Target.AssemblyLocation);

                Assert.Equal(replaced ? NewContent : OriginalContent, actualContent);
            }
        }
    }
}
