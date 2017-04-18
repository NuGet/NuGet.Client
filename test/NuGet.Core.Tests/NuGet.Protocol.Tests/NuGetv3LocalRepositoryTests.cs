﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Repositories.Test
{
    public class NuGetv3LocalRepositoryTests
    {
        [Fact]
        public async Task NuGetv3LocalRepository_FindPackagesById_InstallStress()
        {
            // Arrange
            using (var workingDir = TestDirectory.Create())
            {
                var id = "a";
                var target = new NuGetv3LocalRepository(workingDir);

                var packages = new ConcurrentQueue<PackageIdentity>();
                var limit = 100;

                for (int i = 0; i < limit; i++)
                {
                    packages.Enqueue(new PackageIdentity(id, NuGetVersion.Parse($"{i + 1}.0.0")));
                }

                var tasks = new List<Task>();
                var sem = new ManualResetEventSlim(false);

                for (int i = 0; i < 10; i++)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        sem.Wait();

                        PackageIdentity identity;
                        while (packages.TryDequeue(out identity))
                        {
                                // Fetch
                                var result = target.FindPackagesById(identity.Id)
                                        .FirstOrDefault(f => f.Version == identity.Version);

                            Assert.Null(result);

                                // Create package
                                await SimpleTestPackageUtility.CreateFolderFeedV3(workingDir,
                                        PackageSaveMode.Defaultv3,
                                        identity);

                                // Clear
                                target.ClearCacheForIds(new[] { identity.Id });

                            result = target.FindPackagesById(identity.Id)
                                .FirstOrDefault(f => f.Version == identity.Version);

                                // Assert the package was found
                                Assert.NotNull(result);
                        }
                    }));
                }

                sem.Set();
                await Task.WhenAll(tasks);

                // Assert
                var results2 = target.FindPackagesById(id);
                Assert.Equal(limit, results2.Count());
            }
        }

        [Fact]
        public async Task NuGetv3LocalRepository_FindPackagesById_Stress()
        {
            // Arrange
            using (var workingDir = TestDirectory.Create())
            {
                var id = "a";
                var target = new NuGetv3LocalRepository(workingDir);

                var packages = new List<PackageIdentity>();

                for (int i=0; i < 100; i++)
                {
                    packages.Add(new PackageIdentity(id, NuGetVersion.Parse($"{i + 1}.0.0")));
                }

                await SimpleTestPackageUtility.CreateFolderFeedV3(workingDir, packages.ToArray());

                var tasks = new List<Task>();
                var sem = new ManualResetEventSlim(false);

                for (int i = 0; i < 10; i++)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        sem.Wait();

                        for (int j=0; j < 100; j++)
                        {
                            // Fetch
                            var result = target.FindPackagesById(id);

                            // Assert
                            Assert.Equal(100, result.Count());

                            // Clear
                            for (int k = 0; k < 100; k++)
                            {
                                target.ClearCacheForIds(new[] { id });
                            }
                        }
                    }));
                }

                sem.Set();
                await Task.WhenAll(tasks);

                // Assert
                var results2 = target.FindPackagesById(id);
                Assert.Equal(100, results2.Count());
            }
        }

        [Fact]
        public void NuGetv3LocalRepository_FindPackagesById_ReturnsEmptySequenceWithIdNotFound()
        {
            // Arrange
            using (var workingDir = TestDirectory.Create())
            {
                var target = new NuGetv3LocalRepository(workingDir);

                // Act
                var packages = target.FindPackagesById("Foo");

                // Assert
                Assert.Empty(packages);
            }
        }

        [Fact]
        public async Task NuGetv3LocalRepository_FindPackagesById_UsesProvidedIdCase()
        {
            // Arrange
            using (var workingDir = TestDirectory.Create())
            {
                var id = "Foo";
                var target = new NuGetv3LocalRepository(workingDir);
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    workingDir,
                    PackageSaveMode.Defaultv3,
                    new SimpleTestPackageContext("foo", "1.0.0"));

                // Act
                var packages = target.FindPackagesById(id);

                // Assert
                Assert.Equal(1, packages.Count());
                Assert.Equal(id, packages.ElementAt(0).Id);
                Assert.Equal("1.0.0", packages.ElementAt(0).Version.ToNormalizedString());
            }
        }
        
        [Fact]
        public async Task NuGetv3LocalRepository_FindPackagesById_LeavesVersionCaseFoundOnFileSystem()
        {
            // Arrange
            using (var workingDir = TestDirectory.Create())
            {
                var id = "Foo";
                var target = new NuGetv3LocalRepository(workingDir);
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    workingDir,
                    PackageSaveMode.Defaultv3,
                    new SimpleTestPackageContext(id, "1.0.0"),
                    new SimpleTestPackageContext(id, "2.0.0-Beta"));

                // Act
                var packages = target.FindPackagesById(id);

                // Assert
                Assert.Equal(2, packages.Count());
                packages = packages.OrderBy(x => x.Version);
                Assert.Equal(id, packages.ElementAt(0).Id);
                Assert.Equal("1.0.0", packages.ElementAt(0).Version.ToNormalizedString());
                Assert.Equal(id, packages.ElementAt(1).Id);
                Assert.Equal("2.0.0-beta", packages.ElementAt(1).Version.ToNormalizedString());
            }
        }

        [Fact]
        public void NuGetv3LocalRepository_FindPackage_ReturnsNullWithIdNotFound()
        {
            // Arrange
            using (var workingDir = TestDirectory.Create())
            {
                var target = new NuGetv3LocalRepository(workingDir);

                // Act
                var package = target.FindPackage("Foo", NuGetVersion.Parse("2.0.0-BETA"));

                // Assert
                Assert.Null(package);
            }
        }

        [Fact]
        public async Task NuGetv3LocalRepository_FindPackage_ReturnsNullWithVersionNotFound()
        {
            // Arrange
            using (var workingDir = TestDirectory.Create())
            {
                var id = "Foo";
                var target = new NuGetv3LocalRepository(workingDir);
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    workingDir,
                    PackageSaveMode.Defaultv3,
                    new SimpleTestPackageContext(id, "1.0.0"),
                    new SimpleTestPackageContext(id, "2.0.0-Beta"));

                // Act
                var package = target.FindPackage(id, NuGetVersion.Parse("3.0.0-BETA"));

                // Assert
                Assert.Null(package);
            }
        }

        [Fact]
        public async Task NuGetv3LocalRepository_FindPackage_UsesProvidedVersionCase()
        {
            // Arrange
            using (var workingDir = TestDirectory.Create())
            {
                var id = "Foo";
                var target = new NuGetv3LocalRepository(workingDir);
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    workingDir,
                    PackageSaveMode.Defaultv3,
                    new SimpleTestPackageContext(id, "1.0.0"),
                    new SimpleTestPackageContext(id, "2.0.0-Beta"));

                // Act
                var package = target.FindPackage(id, NuGetVersion.Parse("2.0.0-BETA"));

                // Assert
                Assert.NotNull(package);
                Assert.Equal(id, package.Id);
                Assert.Equal("2.0.0-BETA", package.Version.ToNormalizedString());
            }
        }

        [Fact]
        public async Task NuGetv3LocalRepository_FindPackage_VerifyNuspecsCached()
        {
            // Arrange
            using (var workingDir = TestDirectory.Create())
            {
                var id = "Foo";
                var target = new NuGetv3LocalRepository(workingDir);
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    workingDir,
                    PackageSaveMode.Defaultv3,
                    new SimpleTestPackageContext(id, "1.0.0"),
                    new SimpleTestPackageContext(id, "2.0.0-Beta"));

                // Act
                var package1 = target.FindPackage(id, NuGetVersion.Parse("2.0.0-beta"));
                var package2 = target.FindPackage(id, NuGetVersion.Parse("2.0.0-BETA"));
                var package3 = target.FindPackage(id, NuGetVersion.Parse("2.0.0-beta"));

                // Assert
                Assert.True(ReferenceEquals(package1, package3));
                Assert.True(ReferenceEquals(package1.Nuspec, package2.Nuspec));
                Assert.True(ReferenceEquals(package1.Nuspec, package3.Nuspec));

                // These should contain different versions
                Assert.False(ReferenceEquals(package1, package2));
            }
        }
    }
}
