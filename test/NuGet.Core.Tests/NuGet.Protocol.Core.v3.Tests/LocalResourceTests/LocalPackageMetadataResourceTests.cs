﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class LocalPackageMetadataResourceTests
    {
        [Fact]
        public async Task LocalPackageMetadataResourceTests_GetMetadataStable()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageA1 = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "1.0.0",
                    Dependencies = new List<SimpleTestPackageContext>() { packageX }
                };

                var packageA2 = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "2.0.0-preview.12.4+server.a",
                    Dependencies = new List<SimpleTestPackageContext>() { packageX }
                };

                var packageContexts = new SimpleTestPackageContext[]
                {
                    packageA1,
                    packageA2,
                    packageX
                };

                SimpleTestPackageUtility.CreatePackages(root, packageContexts);

                var localResource = new FindLocalPackagesResourceV2(root);
                var resource = new LocalPackageMetadataResource(localResource);

                // Act
                var results = (await resource.GetMetadataAsync(
                        "A",
                        includePrerelease: false,
                        includeUnlisted: false,
                        log: testLogger,
                        token: CancellationToken.None))
                        .ToList();

                var package = results.Single();

                // Assert
                Assert.Equal("a", package.Identity.Id);
                Assert.Equal("1.0.0", package.Identity.Version.ToFullString());
            }
        }

        [Fact]
        public async Task LocalPackageMetadataResourceTests_GetMetadataPrerelease()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageA1 = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "1.0.0",
                    Dependencies = new List<SimpleTestPackageContext>() { packageX }
                };

                var packageA2 = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "2.0.0-preview.12.4+server.a",
                    Dependencies = new List<SimpleTestPackageContext>() { packageX }
                };

                var packageContexts = new SimpleTestPackageContext[]
                {
                    packageA1,
                    packageA2,
                    packageX
                };

                SimpleTestPackageUtility.CreatePackages(root, packageContexts);

                var localResource = new FindLocalPackagesResourceV2(root);
                var resource = new LocalPackageMetadataResource(localResource);

                // Act
                var results = (await resource.GetMetadataAsync(
                        "A",
                        includePrerelease: true,
                        includeUnlisted: false,
                        log: testLogger,
                        token: CancellationToken.None))
                        .ToList();

                var package = results.OrderByDescending(p => p.Identity.Version).First();

                // Assert
                Assert.Equal("a", package.Identity.Id);
                Assert.Equal("2.0.0-preview.12.4+server.a", package.Identity.Version.ToFullString());
            }
        }

        [Fact]
        public async Task LocalPackageMetadataResourceTests_GetMetadataStableNoVersions()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageA1 = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "1.0.0-alpha",
                    Dependencies = new List<SimpleTestPackageContext>() { packageX }
                };

                var packageA2 = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "2.0.0-preview.12.4+server.a",
                    Dependencies = new List<SimpleTestPackageContext>() { packageX }
                };

                var packageContexts = new SimpleTestPackageContext[]
                {
                    packageA1,
                    packageA2,
                    packageX
                };

                SimpleTestPackageUtility.CreatePackages(root, packageContexts);

                var localResource = new FindLocalPackagesResourceV2(root);
                var resource = new LocalPackageMetadataResource(localResource);

                // Act
                var results = (await resource.GetMetadataAsync(
                        "A",
                        includePrerelease: false,
                        includeUnlisted: false,
                        log: testLogger,
                        token: CancellationToken.None))
                        .ToList();

                // Assert
                Assert.Equal(0, results.Count);
            }
        }

        [Fact]
        public async Task LocalPackageMetadataResourceTests_GetMetadataNoMatch()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var packageContexts = new SimpleTestPackageContext[]
                {
                };

                SimpleTestPackageUtility.CreatePackages(root, packageContexts);

                var localResource = new FindLocalPackagesResourceV2(root);
                var resource = new LocalPackageMetadataResource(localResource);

                // Act
                var results = (await resource.GetMetadataAsync(
                        "A",
                        includePrerelease: false,
                        includeUnlisted: false,
                        log: testLogger,
                        token: CancellationToken.None))
                        .ToList();

                // Assert
                Assert.Equal(0, results.Count);
            }
        }

        [Fact]
        public async Task LocalPackageMetadataResourceTests_VerifyAllFields()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var nuspec = XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                        <metadata>
                            <id>a</id>
                            <version>1.0.0-alpha.1.2+5</version>
                            <title>myTitle</title>
                            <authors>a,b,c</authors>
                            <owners>a,b</owners>
                            <description>package description</description>
                            <releaseNotes>notes</releaseNotes>
                            <summary>sum</summary>
                            <language>en-us</language>
                            <projectUrl>http://nuget.org/</projectUrl>
                            <iconUrl>http://nuget.org/nuget.jpg</iconUrl>
                            <licenseUrl>http://nuget.org/license.txt</licenseUrl>
                            <requireLicenseAcceptance>true</requireLicenseAcceptance>
                            <copyright>MIT</copyright>
                            <tags>a b c</tags>
                            <developmentDependency>true</developmentDependency>
                            <dependencies>
                                <group>
                                 <dependency id=""b"" version=""1.0"" />
                                </group>
                                 <group targetFramework=""net461"">
                                </group>
                            </dependencies>
                        </metadata>
                        </package>");

                var packageA = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "1.0.0-alpha.1.2+5",
                    Nuspec = nuspec
                };

                var packageA2 = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "1.0.0-alpha.1.1"
                };

                var packageContexts = new SimpleTestPackageContext[]
                {
                    packageA,
                    packageA2
                };

                SimpleTestPackageUtility.CreatePackages(root, packageContexts);

                var localResource = new FindLocalPackagesResourceV2(root);
                var resource = new LocalPackageMetadataResource(localResource);

                // Act
                var packages = (await resource.GetMetadataAsync(
                        "A",
                        includePrerelease: true,
                        includeUnlisted: false,
                        log: testLogger,
                        token: CancellationToken.None))
                        .OrderByDescending(p => p.Identity.Version)
                        .ToList();

                var package = packages.First();

                // Assert
                Assert.Equal(2, packages.Count);
                Assert.Equal("a,b,c", package.Authors);
                Assert.Equal(2, package.DependencySets.Count());
                Assert.Equal("package description", package.Description);
                Assert.Equal(0, package.DownloadCount);
                Assert.Equal(new Uri("http://nuget.org/nuget.jpg"), package.IconUrl);
                Assert.Equal("1.0.0-alpha.1.2+5", package.Identity.Version.ToFullString());
                Assert.Equal(new Uri("http://nuget.org/license.txt"), package.LicenseUrl);
                Assert.Equal("a,b", package.Owners);
                Assert.Equal(new Uri("http://nuget.org/"), package.ProjectUrl);
                Assert.NotNull(package.Published);
                Assert.Null(package.ReportAbuseUrl);
                Assert.True(package.RequireLicenseAcceptance);
                Assert.Equal("sum", package.Summary);
                Assert.Equal("a b c", package.Tags);
                Assert.Equal("myTitle", package.Title);
            }
        }
    }
}
