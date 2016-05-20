﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;


namespace NuGet.Protocol.Core.v3.Tests
{
    public class LocalAutoCompleteResourceTests
    {
        [Fact]
        public async Task LocalAutoCompleteResource_IdStartsWithEmptyString()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var packages = new[]
                {
                    new TestLocalPackageInfo("packageA", "1.0.0"),
                    new TestLocalPackageInfo("packagea", "1.0.0-alpha.1.2.3+a.b"),
                    new TestLocalPackageInfo("packageB", "2.0.0")
                };

                var localResource = new TestFindLocalPackagesResource(packages);
                var resource = new LocalAutoCompleteResource(localResource);

                // Act
                var ids = (await resource.IdStartsWith(string.Empty, includePrerelease: true, log: testLogger, token: CancellationToken.None)).ToList();

                // Assert
                Assert.Equal(2, ids.Count);
                Assert.Equal("packageA", ids[0]);
                Assert.Equal("packageB", ids[1]);
            }
        }

        [Fact]
        public async Task LocalAutoCompleteResource_IdStartsWithEmptyStringAndStable()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var packages = new[]
                {
                    new TestLocalPackageInfo("packageA", "1.0.0-beta"),
                    new TestLocalPackageInfo("packagea", "1.0.0-alpha.1.2.3+a.b"),
                    new TestLocalPackageInfo("packageB", "2.0.0")
                };

                var localResource = new TestFindLocalPackagesResource(packages);
                var resource = new LocalAutoCompleteResource(localResource);

                // Act
                var ids = (await resource.IdStartsWith(string.Empty, includePrerelease: false, log: testLogger, token: CancellationToken.None)).ToList();

                // Assert
                Assert.Equal(1, ids.Count);
                Assert.Equal("packageB", ids[0]);
            }
        }

        [Fact]
        public async Task LocalAutoCompleteResource_IdStartsWithFilter()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var packages = new[]
                {
                    new TestLocalPackageInfo("aaa", "1.0.0"),
                    new TestLocalPackageInfo("aab", "1.0.0-alpha.1.2.3+a.b"),
                    new TestLocalPackageInfo("acc", "2.0.0")
                };

                var localResource = new TestFindLocalPackagesResource(packages);
                var resource = new LocalAutoCompleteResource(localResource);

                // Act
                var ids = (await resource.IdStartsWith("aa", includePrerelease: true, log: testLogger, token: CancellationToken.None)).ToList();

                // Assert
                Assert.Equal(2, ids.Count);
                Assert.Equal("aaa", ids[0]);
                Assert.Equal("aab", ids[1]);
            }
        }

        [Fact]
        public async Task LocalAutoCompleteResource_IdStartsWithFilter_NotFound()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var packages = new[]
                {
                    new TestLocalPackageInfo("aaa", "1.0.0"),
                    new TestLocalPackageInfo("aab", "1.0.0-alpha.1.2.3+a.b"),
                    new TestLocalPackageInfo("acc", "2.0.0")
                };

                var localResource = new TestFindLocalPackagesResource(packages);
                var resource = new LocalAutoCompleteResource(localResource);

                // Act
                var ids = (await resource.IdStartsWith("z", includePrerelease: true, log: testLogger, token: CancellationToken.None)).ToList();

                // Assert
                Assert.Equal(0, ids.Count);
            }
        }

        [Fact]
        public async Task LocalAutoCompleteResource_IdStartsWithFilter_StableNotFound()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var packages = new[]
                {
                    new TestLocalPackageInfo("aaa", "1.0.0-beta"),
                    new TestLocalPackageInfo("aab", "1.0.0-alpha.1.2.3+a.b"),
                    new TestLocalPackageInfo("acc", "2.0.0-alpha")
                };

                var localResource = new TestFindLocalPackagesResource(packages);
                var resource = new LocalAutoCompleteResource(localResource);

                // Act
                var ids = (await resource.IdStartsWith("a", includePrerelease: false, log: testLogger, token: CancellationToken.None)).ToList();

                // Assert
                Assert.Equal(0, ids.Count);
            }
        }

        [Fact]
        public async Task LocalAutoCompleteResource_VersionStartsWithEmptyString()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var packages = new[]
                {
                    new TestLocalPackageInfo("packageA", "1.0.0"),
                    new TestLocalPackageInfo("packagea", "1.0.0-alpha.1.2.3+a.b"),
                    new TestLocalPackageInfo("packageB", "2.0.0")
                };

                var localResource = new TestFindLocalPackagesResource(packages);
                var resource = new LocalAutoCompleteResource(localResource);

                // Act
                var versions = (await resource.VersionStartsWith("packageA", string.Empty, includePrerelease: true, log: testLogger, token: CancellationToken.None)).ToList();

                // Assert
                Assert.Equal(2, versions.Count);
                Assert.Equal("1.0.0", versions[0].ToFullString());
                Assert.Equal("1.0.0-alpha.1.2.3+a.b", versions[1].ToFullString());
            }
        }

        [Fact]
        public async Task LocalAutoCompleteResource_VersionStartsWithFilter()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var packages = new[]
                {
                    new TestLocalPackageInfo("packageA", "1.0.0"),
                    new TestLocalPackageInfo("packagea", "1.1.0-alpha.1.2.3+a.b"),
                    new TestLocalPackageInfo("packageB", "2.0.0")
                };

                var localResource = new TestFindLocalPackagesResource(packages);
                var resource = new LocalAutoCompleteResource(localResource);

                // Act
                var versions = (await resource.VersionStartsWith("packageA", "1.0", includePrerelease: true, log: testLogger, token: CancellationToken.None)).ToList();

                // Assert
                Assert.Equal(1, versions.Count);
                Assert.Equal("1.0.0", versions[0].ToFullString());
            }
        }

        [Fact]
        public async Task LocalAutoCompleteResource_VersionStartsWithFilterExactMatch()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var packages = new[]
                {
                    new TestLocalPackageInfo("packageA", "1.0.0"),
                    new TestLocalPackageInfo("packagea", "1.1.0-alpha.1.2.3+a.b"),
                    new TestLocalPackageInfo("packageB", "2.0.0")
                };

                var localResource = new TestFindLocalPackagesResource(packages);
                var resource = new LocalAutoCompleteResource(localResource);

                // Act
                var versions = (await resource.VersionStartsWith("packageA", "1.1.0-alpha.1.2.3+a.b", includePrerelease: true, log: testLogger, token: CancellationToken.None)).ToList();

                // Assert
                Assert.Equal(1, versions.Count);
                Assert.Equal("1.1.0-alpha.1.2.3+a.b", versions[0].ToFullString());
            }
        }

        [Fact]
        public async Task LocalAutoCompleteResource_VersionStartsWithFilterExactMatch_Stable()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var packages = new[]
                {
                    new TestLocalPackageInfo("packageA", "1.0.0"),
                    new TestLocalPackageInfo("packagea", "1.1.0-alpha.1.2.3+a.b"),
                    new TestLocalPackageInfo("packageB", "2.0.0")
                };

                var localResource = new TestFindLocalPackagesResource(packages);
                var resource = new LocalAutoCompleteResource(localResource);

                // Act
                var versions = (await resource.VersionStartsWith("packageA", "1.1.0-alpha.1.2.3+a.b", includePrerelease: false, log: testLogger, token: CancellationToken.None)).ToList();

                // Assert
                Assert.Equal(0, versions.Count);
            }
        }

        [Fact]
        public async Task LocalAutoCompleteResource_EmptyRepo()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var packages = new TestLocalPackageInfo[]
                {
                };

                var localResource = new TestFindLocalPackagesResource(packages);
                var resource = new LocalAutoCompleteResource(localResource);

                // Act
                var ids = (await resource.IdStartsWith(string.Empty, includePrerelease: true, log: testLogger, token: CancellationToken.None)).ToList();
                var versions = (await resource.VersionStartsWith(string.Empty, string.Empty, includePrerelease: true, log: testLogger, token: CancellationToken.None)).ToList();

                // Assert
                Assert.Equal(0, ids.Count);
                Assert.Equal(0, versions.Count);
            }
        }
    }
}
