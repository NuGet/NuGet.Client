// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.ProjectModel;
using Xunit;

namespace NuGet.Test
{
    public class DependencyGraphSpecTests
    {
        [Fact]
        public void WithReplacedSpec()
        {
            // Arrange
            var packageSpecA = new PackageSpec();
            packageSpecA.Title = "A";
            packageSpecA.RestoreMetadata = new ProjectRestoreMetadata() { ProjectUniqueName = "a", CentralPackageVersionsEnabled = false };
            var dgSpec = new DependencyGraphSpec();
            var packageSpecB = new PackageSpec();
            packageSpecB.Title = "B";
            packageSpecB.RestoreMetadata = new ProjectRestoreMetadata()
            {
                ProjectUniqueName = "BBB"
            };
            var packageSpecC = new PackageSpec();
            packageSpecC.Title = "C";
            packageSpecC.RestoreMetadata = new ProjectRestoreMetadata()
            {
                ProjectUniqueName = "CCC"
            };

            // Act
            dgSpec = dgSpec.WithReplacedSpec(packageSpecA);
            dgSpec = dgSpec.WithReplacedSpec(packageSpecB);
            dgSpec = dgSpec.WithReplacedSpec(packageSpecC);

            // Assert
            Assert.Equal(dgSpec.Projects.Count, 3);
            Assert.Equal(dgSpec.Restore.Count, 1);
        }

        [Fact]
        public void WithPackageSpecs()
        {
            // Arrange
            var packageSpecA = new PackageSpec();
            packageSpecA.Title = "A";
            packageSpecA.RestoreMetadata = new ProjectRestoreMetadata() { ProjectUniqueName = "a", CentralPackageVersionsEnabled = false };
            var dgSpec = new DependencyGraphSpec();
            var packageSpecB = new PackageSpec();
            packageSpecB.Title = "B";
            packageSpecB.RestoreMetadata = new ProjectRestoreMetadata()
            {
                ProjectUniqueName = "BBB"
            };
            var packageSpecC = new PackageSpec();
            packageSpecC.Title = "C";
            packageSpecC.RestoreMetadata = new ProjectRestoreMetadata()
            {
                ProjectUniqueName = "CCC"
            };
            var newNugetPackageSpecs = new List<PackageSpec>()
            {
                packageSpecA,
                packageSpecB,
                packageSpecC
            };

            // Act
            dgSpec = dgSpec.WithPackageSpecs(newNugetPackageSpecs);

            // Assert
            Assert.Equal(dgSpec.Projects.Count, 3);
            Assert.Equal(dgSpec.Restore.Count, 3);
        }
    }
}
