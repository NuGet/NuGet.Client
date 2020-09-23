// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.CommandLine.XPlat;
using NuGet.CommandLine.XPlat.Utility;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class ListPackageCommandRunnerTests
    {
        public class TopLevelPackagesFilterForOutdated
        {
            [Fact]
            public void FiltersAutoReferencedPackages()
            {
                // Arrange
                Func<InstalledPackageReference, bool> filter = ListPackageHelper.TopLevelPackagesFilterForOutdated;
                var installedPackageReference = ListPackageTestHelper.CreateInstalledPackageReference(autoReference: true);

                // Act
                bool result = filter.Invoke(installedPackageReference);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public void DoesNotFilterPackagesWithLatestMetadataNull()
            {
                // Arrange
                Func<InstalledPackageReference, bool> filter = ListPackageHelper.TopLevelPackagesFilterForOutdated;
                var installedPackageReference = ListPackageTestHelper.CreateInstalledPackageReference();
                installedPackageReference.LatestPackageMetadata = null;

                // Act
                bool result = filter.Invoke(installedPackageReference);

                // Assert
                Assert.True(result);
            }

            [Fact]
            public void DoesNotFilterPackagesWithNewerVersionAvailable()
            {
                // Arrange
                Func<InstalledPackageReference, bool> filter = ListPackageHelper.TopLevelPackagesFilterForOutdated;
                var installedPackageReference = ListPackageTestHelper.CreateInstalledPackageReference(
                    latestPackageVersionString: "2.0.0");

                // Act
                bool result = filter.Invoke(installedPackageReference);

                // Assert
                Assert.True(result);
            }
        }

        public class TransitivePackagesFilterForOutdated
        {
            [Fact]
            public void DoesNotFilterPackagesWithLatestMetadataNull()
            {
                // Arrange
                Func<InstalledPackageReference, bool> filter = ListPackageHelper.TransitivePackagesFilterForOutdated;
                var installedPackageReference = ListPackageTestHelper.CreateInstalledPackageReference();
                installedPackageReference.LatestPackageMetadata = null;

                // Act
                bool result = filter.Invoke(installedPackageReference);

                // Assert
                Assert.True(result);
            }

            [Fact]
            public void DoesNotFilterPackagesWithNewerVersionAvailable()
            {
                // Arrange
                Func<InstalledPackageReference, bool> filter = ListPackageHelper.TransitivePackagesFilterForOutdated;
                var installedPackageReference = ListPackageTestHelper.CreateInstalledPackageReference(
                    latestPackageVersionString: "2.0.0");

                // Act
                bool result = filter.Invoke(installedPackageReference);

                // Assert
                Assert.True(result);
            }
        }

        public class PackagesFilterForDeprecated
        {
            [Fact]
            public void FiltersPackagesWithoutDeprecationMetadata()
            {
                // Arrange
                Func<InstalledPackageReference, bool> filter = ListPackageHelper.PackagesFilterForDeprecated;
                var installedPackageReference = ListPackageTestHelper.CreateInstalledPackageReference();

                // Act
                bool result = filter.Invoke(installedPackageReference);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public void DoesNotFilterPackagesWithDeprecationMetadata()
            {
                // Arrange
                Func<InstalledPackageReference, bool> filter = ListPackageHelper.PackagesFilterForDeprecated;
                var installedPackageReference = ListPackageTestHelper.CreateInstalledPackageReference(isDeprecated: true);

                // Act
                bool result = filter.Invoke(installedPackageReference);

                // Assert
                Assert.True(result);
            }
        }

        public class PackagesFilterForVulnerable
        {
            [Fact]
            public void FiltersPackagesWithoutVulnerableMetadata()
            {
                // Arrange
                Func<InstalledPackageReference, bool> filter = ListPackageHelper.PackagesFilterForVulnerable;
                var installedPackageReference = ListPackageTestHelper.CreateInstalledPackageReference();

                // Act
                bool result = filter.Invoke(installedPackageReference);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public void DoesNotFilterPackagesWithVulnerableMetadata()
            {
                // Arrange
                Func<InstalledPackageReference, bool> filter = ListPackageHelper.PackagesFilterForVulnerable;
                var installedPackageReference = ListPackageTestHelper.CreateInstalledPackageReference(vulnerabilityCount: 1);

                // Act
                bool result = filter.Invoke(installedPackageReference);

                // Assert
                Assert.True(result);
            }
        }
    }
}
