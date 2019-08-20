// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Moq;
using NuGet.CommandLine.XPlat;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class ListPackageCommandRunnerTests
    {
        public class TopLevelPackagesFilterForOutdated
        {
            [Fact]
            public async Task FiltersAutoReferencedPackages()
            {
                // Arrange
                var filter = ListPackageCommandRunner.TopLevelPackagesFilterForOutdated;
                var installedPackageReference = CreateInstalledPackageReference(autoReference: true);

                // Act
                var result = await filter.Invoke(installedPackageReference);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public async Task DoesNotFilterPackagesWithLatestMetadataNull()
            {
                // Arrange
                var filter = ListPackageCommandRunner.TopLevelPackagesFilterForOutdated;
                var installedPackageReference = CreateInstalledPackageReference();
                installedPackageReference.LatestPackageMetadata = null;

                // Act
                var result = await filter.Invoke(installedPackageReference);

                // Assert
                Assert.True(result);
            }

            [Fact]
            public async Task DoesNotFilterPackagesWithNewerVersionAvailable()
            {
                // Arrange
                var filter = ListPackageCommandRunner.TopLevelPackagesFilterForOutdated;
                var installedPackageReference = CreateInstalledPackageReference(
                    latestPackageVersionString: "2.0.0");

                // Act
                var result = await filter.Invoke(installedPackageReference);

                // Assert
                Assert.True(result);
            }
        }

        public class TransitivePackagesFilterForOutdated
        {
            [Fact]
            public async Task DoesNotFilterPackagesWithLatestMetadataNull()
            {
                // Arrange
                var filter = ListPackageCommandRunner.TransitivePackagesFilterForOutdated;
                var installedPackageReference = CreateInstalledPackageReference();
                installedPackageReference.LatestPackageMetadata = null;

                // Act
                var result = await filter.Invoke(installedPackageReference);

                // Assert
                Assert.True(result);
            }

            [Fact]
            public async Task DoesNotFilterPackagesWithNewerVersionAvailable()
            {
                // Arrange
                var filter = ListPackageCommandRunner.TransitivePackagesFilterForOutdated;
                var installedPackageReference = CreateInstalledPackageReference(
                    latestPackageVersionString: "2.0.0");

                // Act
                var result = await filter.Invoke(installedPackageReference);

                // Assert
                Assert.True(result);
            }
        }

        public class TopLevelPackagesFilterForDeprecated
        {
            [Fact]
            public async Task FiltersPackagesWithoutDeprecationMetadata()
            {
                // Arrange
                var filter = ListPackageCommandRunner.TopLevelPackagesFilterForDeprecated;
                var installedPackageReference = CreateInstalledPackageReference();

                // Act
                var result = await filter.Invoke(installedPackageReference);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public async Task DoesNotFilterPackagesWithDeprecationMetadata()
            {
                // Arrange
                var filter = ListPackageCommandRunner.TopLevelPackagesFilterForDeprecated;
                var installedPackageReference = CreateInstalledPackageReference(isDeprecated: true);

                // Act
                var result = await filter.Invoke(installedPackageReference);

                // Assert
                Assert.True(result);
            }
        }

        public class TransitivePackagesFilterForDeprecated
        {
            [Fact]
            public async Task FiltersPackagesWithoutDeprecationMetadata()
            {
                // Arrange
                var filter = ListPackageCommandRunner.TransitivePackagesFilterForDeprecated;
                var installedPackageReference = CreateInstalledPackageReference();

                // Act
                var result = await filter.Invoke(installedPackageReference);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public async Task DoesNotFilterPackagesWithDeprecationMetadata()
            {
                // Arrange
                var filter = ListPackageCommandRunner.TransitivePackagesFilterForDeprecated;
                var installedPackageReference = CreateInstalledPackageReference(isDeprecated: true);

                // Act
                var result = await filter.Invoke(installedPackageReference);

                // Assert
                Assert.True(result);
            }
        }

        private static InstalledPackageReference CreateInstalledPackageReference(
            bool autoReference = false,
            bool isDeprecated = false,
            string resolvedPackageVersionString = "1.0.0",
            string latestPackageVersionString = "2.0.0")
        {
            const string packageId = "Package.Id";
            var latestPackageVersion = new NuGetVersion(latestPackageVersionString);
            var resolvedPackageVersion = new NuGetVersion(resolvedPackageVersionString);

            var resolvedPackageMetadata = new Mock<IPackageSearchMetadata>();
            resolvedPackageMetadata.Setup(m => m.Identity).Returns(new PackageIdentity(packageId, resolvedPackageVersion));
            if (isDeprecated)
            {
                resolvedPackageMetadata
                    .Setup(m => m.GetDeprecationMetadataAsync())
                    .ReturnsAsync(new PackageDeprecationMetadata());
            }

            var installedPackageReference = new InstalledPackageReference(packageId)
            {
                AutoReference = autoReference,

                LatestPackageMetadata = PackageSearchMetadataBuilder
                .FromIdentity(new PackageIdentity(packageId, latestPackageVersion))
                .Build(),

                ResolvedPackageMetadata = resolvedPackageMetadata.Object
            };

            return installedPackageReference;
        }
    }
}
