// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class PackageSpecReferenceDependencyProviderTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GetSpecDependencies_AddsCentralPackageVersionsIfDefined(bool cpvmEnabled)
        {
            // Arrange
            var logger = new TestLogger();
            var dependencyFoo = new LibraryDependency(new LibraryRange("foo", versionRange: null, LibraryDependencyTarget.Package),
               LibraryDependencyType.Default,
               LibraryIncludeFlags.All,
               LibraryIncludeFlags.All,
               new List<Common.NuGetLogCode>(),
               autoReferenced: false,
               generatePathProperty: true);

            var centralVersionFoo = new CentralPackageVersion("foo", VersionRange.Parse("2.0.0"));
            var centralVersionBar = new CentralPackageVersion("bar", VersionRange.Parse("2.0.0"));

            var tfi = CreateTargetFrameworkInformation(new List<LibraryDependency>() { dependencyFoo }, new List<CentralPackageVersion>() { centralVersionFoo, centralVersionBar });
            var dependencyGraphSpec = CreateDependencyGraphSpecWithCentralDependencies(cpvmEnabled, tfi);
            var packSpec = dependencyGraphSpec.Projects[0];

            // Act
            var dependencies = PackageSpecReferenceDependencyProvider.GetSpecDependencies(packSpec, tfi.FrameworkName);

            // Assert
            if(cpvmEnabled)
            {
                Assert.Equal(2, dependencies.Count);
                var barDep = dependencies.Where(d => d.Name == "bar").First();
                Assert.NotNull(barDep);
                Assert.True(barDep.VersionCentrallyManaged);
                Assert.Equal("[2.0.0, )", barDep.LibraryRange.VersionRange.ToNormalizedString());
                var fooDep = dependencies.Where(d => d.Name == "foo").First();
                Assert.NotNull(fooDep);
                Assert.False(fooDep.VersionCentrallyManaged);
                Assert.True(fooDep.VersionCentrallyManaged);
                Assert.Equal("[2.0.0, )", fooDep.LibraryRange.VersionRange.ToNormalizedString());
            }
            else
            {
                Assert.Equal(1, dependencies.Count);
                var fooDep = dependencies.Where(d => d.Name == "foo").First();
                Assert.NotNull(fooDep);
                Assert.False(fooDep.VersionCentrallyManaged);
                Assert.False(fooDep.VersionCentrallyManaged);
                Assert.Null( fooDep.LibraryRange.VersionRange);
            }
        }

        private static TargetFrameworkInformation CreateTargetFrameworkInformation(List<LibraryDependency> dependencies, List<CentralPackageVersion> centralVersionsDependencies)
        {
            NuGetFramework nugetFramework = new NuGetFramework("net40");

            TargetFrameworkInformation tfi = new TargetFrameworkInformation()
            {
                AssetTargetFallback = true,
                Warn = false,
                FrameworkName = nugetFramework,
                Dependencies = dependencies,
            };

            foreach (var cvd in centralVersionsDependencies)
            {
                tfi.CentralPackageVersions.Add(cvd.Name, cvd);
            }

            return tfi;
        }

        private static DependencyGraphSpec CreateDependencyGraphSpecWithCentralDependencies(bool cpvmEnabled, params TargetFrameworkInformation[] tfis)
        {
            var packageSpec = new PackageSpec(tfis);
            packageSpec.RestoreMetadata = new ProjectRestoreMetadata() { ProjectUniqueName = "a", CentralPackageVersionsEnabled = cpvmEnabled };
            var dgSpec = new DependencyGraphSpec();
            dgSpec.AddRestore("a");
            dgSpec.AddProject(packageSpec);
            return dgSpec;
        }
    }
}
