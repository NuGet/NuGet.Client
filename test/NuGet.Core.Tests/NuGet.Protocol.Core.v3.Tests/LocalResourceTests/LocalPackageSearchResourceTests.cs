using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class LocalPackageSearchResourceTests
    {
        [Fact]
        public async Task LocalPackageSearchResource_MatchOnId()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var nuspec = XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                        <metadata>
                            <id>myPackage</id>
                            <version>1.0.0-alpha.1.2+5</version>
                            <description>package description</description>
                            <tags>a b c</tags>
                        </metadata>
                        </package>");

                var packageA = new SimpleTestPackageContext()
                {
                    Id = "myPackage",
                    Version = "1.0.0-alpha.1.2+5",
                    Nuspec = nuspec
                };

                var nuspec2 = XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                        <metadata>
                            <id>myOtherPackage</id>
                            <version>1.0.0-alpha.1.3+5</version>
                            <description>package description</description>
                            <tags>a b c</tags>
                        </metadata>
                        </package>");

                var packageA2 = new SimpleTestPackageContext()
                {
                    Id = "myOtherPackage",
                    Version = "1.0.0-alpha.1.3+5",
                    Nuspec = nuspec2
                };

                var packageContexts = new SimpleTestPackageContext[]
                {
                    packageA,
                    packageA2
                };

                SimpleTestPackageUtility.CreatePackages(root, packageContexts);

                var localResource = new FindLocalPackagesResourceV2(root);
                var resource = new LocalPackageSearchResource(localResource);

                var filter = new SearchFilter()
                {
                    IncludePrerelease = true
                };

                // Act
                var packages = (await resource.SearchAsync(
                        "mypackage",
                        filter,
                        skip: 0,
                        take: 30,
                        log: testLogger,
                        token: CancellationToken.None))
                        .OrderBy(p => p.Identity.Id)
                        .ToList();

                var package = packages.First();

                // Assert
                Assert.Equal(1, packages.Count);
                Assert.Equal("myPackage", package.Identity.Id);
                Assert.Equal("1.0.0-alpha.1.2+5", package.Identity.Version.ToFullString());
            }
        }

        [Fact]
        public async Task LocalPackageSearchResource_RelativePathIsRejected()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();
                var source = Path.Combine("..", "packages");
                var localResource = new FindLocalPackagesResourceV2(source);
                var resource = new LocalPackageSearchResource(localResource);

                var filter = new SearchFilter()
                {
                    IncludePrerelease = true
                };

                // Act & Assert
                var actual = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => resource.SearchAsync(
                        "mypackage",
                        filter,
                        skip: 0,
                        take: 30,
                        log: testLogger,
                        token: CancellationToken.None));
                Assert.Equal(
                    $"The path '{source}' for the selected source could not be resolved.",
                    actual.Message);
            }
        }

        [Fact]
        public async Task LocalPackageSearchResource_MatchOnTag()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var nuspec = XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                        <metadata>
                            <id>myPackage</id>
                            <version>1.0.0-alpha.1.2+5</version>
                            <description>package description</description>
                            <tags>apple orange</tags>
                        </metadata>
                        </package>");

                var packageA = new SimpleTestPackageContext()
                {
                    Id = "myPackage",
                    Version = "1.0.0-alpha.1.2+5",
                    Nuspec = nuspec
                };

                var nuspec2 = XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                        <metadata>
                            <id>myOtherPackage</id>
                            <version>1.0.0-alpha.1.3+5</version>
                            <description>package description</description>
                            <tags>grape</tags>
                        </metadata>
                        </package>");

                var packageA2 = new SimpleTestPackageContext()
                {
                    Id = "myOtherPackage",
                    Version = "1.0.0-alpha.1.3+5",
                    Nuspec = nuspec2
                };

                var packageContexts = new SimpleTestPackageContext[]
                {
                    packageA,
                    packageA2
                };

                SimpleTestPackageUtility.CreatePackages(root, packageContexts);

                var localResource = new FindLocalPackagesResourceV2(root);
                var resource = new LocalPackageSearchResource(localResource);

                var filter = new SearchFilter()
                {
                    IncludePrerelease = true
                };

                // Act
                var packages = (await resource.SearchAsync(
                        "apple",
                        filter,
                        skip: 0,
                        take: 30,
                        log: testLogger,
                        token: CancellationToken.None))
                        .OrderBy(p => p.Identity.Id)
                        .ToList();

                var package = packages.First();

                // Assert
                Assert.Equal(1, packages.Count);
                Assert.Equal("myPackage", package.Identity.Id);
                Assert.Equal("1.0.0-alpha.1.2+5", package.Identity.Version.ToFullString());
            }
        }

        [Fact]
        public async Task LocalPackageSearchResource_MatchOnDescription()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var nuspec = XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                        <metadata>
                            <id>myPackage</id>
                            <version>1.0.0-alpha.1.2+5</version>
                            <description>package blue description</description>
                            <tags>apple orange</tags>
                        </metadata>
                        </package>");

                var packageA = new SimpleTestPackageContext()
                {
                    Id = "myPackage",
                    Version = "1.0.0-alpha.1.2+5",
                    Nuspec = nuspec
                };

                var nuspec2 = XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                        <metadata>
                            <id>myOtherPackage</id>
                            <version>1.0.0-alpha.1.3+5</version>
                            <description>package red description</description>
                            <tags>grape</tags>
                        </metadata>
                        </package>");

                var packageA2 = new SimpleTestPackageContext()
                {
                    Id = "myOtherPackage",
                    Version = "1.0.0-alpha.1.3+5",
                    Nuspec = nuspec2
                };

                var packageContexts = new SimpleTestPackageContext[]
                {
                    packageA,
                    packageA2
                };

                SimpleTestPackageUtility.CreatePackages(root, packageContexts);

                var localResource = new FindLocalPackagesResourceV2(root);
                var resource = new LocalPackageSearchResource(localResource);

                var filter = new SearchFilter()
                {
                    IncludePrerelease = true
                };

                // Act
                var packages = (await resource.SearchAsync(
                        "rEd",
                        filter,
                        skip: 0,
                        take: 30,
                        log: testLogger,
                        token: CancellationToken.None))
                        .OrderBy(p => p.Identity.Id)
                        .ToList();

                var package = packages.First();

                // Assert
                Assert.Equal(1, packages.Count);
                Assert.Equal("myOtherPackage", package.Identity.Id);
                Assert.Equal("1.0.0-alpha.1.3+5", package.Identity.Version.ToFullString());
            }
        }

        [Fact]
        public async Task LocalPackageSearchResource_MatchOnPartialId()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var nuspec = XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                        <metadata>
                            <id>myPackage</id>
                            <version>1.0.0-alpha.1.2+5</version>
                            <description>package description</description>
                            <tags>a b c</tags>
                        </metadata>
                        </package>");

                var packageA = new SimpleTestPackageContext()
                {
                    Id = "myPackage",
                    Version = "1.0.0-alpha.1.2+5",
                    Nuspec = nuspec
                };

                var packageContexts = new SimpleTestPackageContext[]
                {
                    packageA
                };

                SimpleTestPackageUtility.CreatePackages(root, packageContexts);

                var localResource = new FindLocalPackagesResourceV2(root);
                var resource = new LocalPackageSearchResource(localResource);

                var filter = new SearchFilter()
                {
                    IncludePrerelease = true
                };

                // Act
                var packages = (await resource.SearchAsync(
                        "ypac",
                        filter,
                        skip: 0,
                        take: 30,
                        log: testLogger,
                        token: CancellationToken.None))
                        .OrderBy(p => p.Identity.Id)
                        .ToList();

                var package = packages.First();

                // Assert
                Assert.Equal(1, packages.Count);
                Assert.Equal("myPackage", package.Identity.Id);
                Assert.Equal("1.0.0-alpha.1.2+5", package.Identity.Version.ToFullString());
            }
        }

        [Fact]
        public async Task LocalPackageSearchResource_MatchNone()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var nuspec = XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                        <metadata>
                            <id>myPackage</id>
                            <version>1.0.0-alpha.1.2+5</version>
                            <description>package description</description>
                            <tags>a b c</tags>
                        </metadata>
                        </package>");

                var packageA = new SimpleTestPackageContext()
                {
                    Id = "myPackage",
                    Version = "1.0.0-alpha.1.2+5",
                    Nuspec = nuspec
                };

                var packageContexts = new SimpleTestPackageContext[]
                {
                    packageA
                };

                SimpleTestPackageUtility.CreatePackages(root, packageContexts);

                var localResource = new FindLocalPackagesResourceV2(root);
                var resource = new LocalPackageSearchResource(localResource);

                var filter = new SearchFilter()
                {
                    IncludePrerelease = true
                };

                // Act
                var packages = (await resource.SearchAsync(
                        "test",
                        filter,
                        skip: 0,
                        take: 30,
                        log: testLogger,
                        token: CancellationToken.None))
                        .OrderBy(p => p.Identity.Id)
                        .ToList();

                // Assert
                Assert.Equal(0, packages.Count);
            }
        }

        [Fact]
        public async Task LocalPackageSearchResource_SearchStableNoMatch()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var nuspec = XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                        <metadata>
                            <id>myPackage</id>
                            <version>1.0.0-alpha.1.2+5</version>
                            <description>package description</description>
                            <tags>a b c</tags>
                        </metadata>
                        </package>");

                var packageA = new SimpleTestPackageContext()
                {
                    Id = "myPackage",
                    Version = "1.0.0-alpha.1.2+5",
                    Nuspec = nuspec
                };

                var packageContexts = new SimpleTestPackageContext[]
                {
                    packageA
                };

                SimpleTestPackageUtility.CreatePackages(root, packageContexts);

                var localResource = new FindLocalPackagesResourceV2(root);
                var resource = new LocalPackageSearchResource(localResource);

                var filter = new SearchFilter()
                {
                    IncludePrerelease = false
                };

                // Act
                var packages = (await resource.SearchAsync(
                        "ypac",
                        filter,
                        skip: 0,
                        take: 30,
                        log: testLogger,
                        token: CancellationToken.None))
                        .OrderBy(p => p.Identity.Id)
                        .ToList();

                // Assert
                Assert.Equal(0, packages.Count);
            }
        }

        [Fact]
        public async Task LocalPackageSearchResource_SearchStableMatch()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var nuspec = XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                        <metadata>
                            <id>myPackage</id>
                            <version>2.0.0-alpha.1.2+5</version>
                            <description>package description</description>
                            <tags>a b c</tags>
                        </metadata>
                        </package>");

                var packageA = new SimpleTestPackageContext()
                {
                    Id = "myPackage",
                    Version = "1.0.0-alpha.1.2+5",
                    Nuspec = nuspec
                };

                var nuspec2 = XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                        <metadata>
                            <id>myPackageB</id>
                            <version>1.0.0</version>
                            <description>package description</description>
                            <tags>a b c</tags>
                        </metadata>
                        </package>");

                var packageA2 = new SimpleTestPackageContext()
                {
                    Id = "myPackageB",
                    Version = "1.0.0",
                    Nuspec = nuspec2
                };

                var packageContexts = new SimpleTestPackageContext[]
                {
                    packageA,
                    packageA2
                };

                SimpleTestPackageUtility.CreatePackages(root, packageContexts);

                var localResource = new FindLocalPackagesResourceV2(root);
                var resource = new LocalPackageSearchResource(localResource);

                var filter = new SearchFilter()
                {
                    IncludePrerelease = false
                };

                // Act
                var packages = (await resource.SearchAsync(
                        "mypackage",
                        filter,
                        skip: 0,
                        take: 30,
                        log: testLogger,
                        token: CancellationToken.None))
                        .OrderBy(p => p.Identity.Id)
                        .ToList();

                var package = packages.First();

                // Assert
                Assert.Equal(1, packages.Count);
                Assert.Equal("myPackageB", package.Identity.Id);
                Assert.Equal("1.0.0", package.Identity.Version.ToFullString());
            }
        }
    }
}
