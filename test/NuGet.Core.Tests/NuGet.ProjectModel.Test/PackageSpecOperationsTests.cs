// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class PackageSpecOperationsTests
    {
        [Fact]
        public void AddOrUpdateDependency_AddsNewDependencyToAllFrameworks()
        {
            // Arrange
            var spec = new PackageSpec(new[]
            {
                new TargetFrameworkInformation
                {
                    FrameworkName = FrameworkConstants.CommonFrameworks.Net45
                }
            });
            var identity = new PackageIdentity("NuGet.Versioning", new NuGetVersion("1.0.0"));

            // Act
            PackageSpecOperations.AddOrUpdateDependency(spec, identity);

            // Assert
            Assert.Equal(1, spec.Dependencies.Count);
            Assert.Empty(spec.TargetFrameworks[0].Dependencies);
            Assert.Equal(identity.Id, spec.Dependencies[0].LibraryRange.Name);
            Assert.Equal(identity.Version, spec.Dependencies[0].LibraryRange.VersionRange.MinVersion);
        }

        [Fact]
        public void AddOrUpdateDependency_UpdatesExistingDependencies()
        {
            // Arrange
            var frameworkA = new TargetFrameworkInformation
            {
                FrameworkName = FrameworkConstants.CommonFrameworks.Net45
            };
            frameworkA.Dependencies.Add(new LibraryDependency
            {
                LibraryRange = new LibraryRange
                {
                    Name = "nuget.versioning",
                    VersionRange = new VersionRange(new NuGetVersion("0.9.0"))
                }
            });
            var frameworkB = new TargetFrameworkInformation
            {
                FrameworkName = FrameworkConstants.CommonFrameworks.NetStandard16
            };
            frameworkB.Dependencies.Add(new LibraryDependency
            {
                LibraryRange = new LibraryRange
                {
                    Name = "NUGET.VERSIONING",
                    VersionRange = new VersionRange(new NuGetVersion("0.8.0"))
                }
            });
            var spec = new PackageSpec(new[] { frameworkA, frameworkB });
            var identity = new PackageIdentity("NuGet.Versioning", new NuGetVersion("1.0.0"));

            // Act
            PackageSpecOperations.AddOrUpdateDependency(spec, identity);

            // Assert
            Assert.Empty(spec.Dependencies);

            Assert.Equal(1, spec.TargetFrameworks[0].Dependencies.Count);
            Assert.Equal("nuget.versioning", spec.TargetFrameworks[0].Dependencies[0].LibraryRange.Name);
            Assert.Equal(
                identity.Version,
                spec.TargetFrameworks[0].Dependencies[0].LibraryRange.VersionRange.MinVersion);

            Assert.Equal(1, spec.TargetFrameworks[1].Dependencies.Count);
            Assert.Equal("NUGET.VERSIONING", spec.TargetFrameworks[1].Dependencies[0].LibraryRange.Name);
            Assert.Equal(
                identity.Version,
                spec.TargetFrameworks[1].Dependencies[0].LibraryRange.VersionRange.MinVersion);
        }

        [Fact]
        public void AddDependency_ToSpecificFrameworks_RejectsExistingDependencies() // TODO NK - bad test, Add tests for the End-To-End scenario and make sure all of the package Spec operations and properly and well tested.
        {
            // Arrange
            var frameworkA = new TargetFrameworkInformation
            {
                FrameworkName = FrameworkConstants.CommonFrameworks.Net45
            };
            var frameworkB = new TargetFrameworkInformation
            {
                FrameworkName = FrameworkConstants.CommonFrameworks.NetStandard16
            };
            var spec = new PackageSpec(new[] { frameworkA, frameworkB });
            var identity = new PackageIdentity("NuGet.Versioning", new NuGetVersion("1.0.0"));

            // Act
            PackageSpecOperations.AddOrUpdateDependency(
                spec,
                identity,
                new[] { frameworkB.FrameworkName });

            // Assert
            Assert.Empty(spec.Dependencies);

            Assert.Empty(spec.TargetFrameworks[0].Dependencies);

            Assert.Equal(1, spec.TargetFrameworks[1].Dependencies.Count);
            Assert.Equal(identity.Id, spec.TargetFrameworks[1].Dependencies[0].LibraryRange.Name);
            Assert.Equal(
                identity.Version,
                spec.TargetFrameworks[1].Dependencies[0].LibraryRange.VersionRange.MinVersion);
        }


        [Fact]
        public void AddDependency_ToSpecificFrameworks_UpdatesExistingDependencies()
        {
            // Arrange
            var identity = new PackageIdentity("NuGet.Versioning", new NuGetVersion("2.0.0"));

            var frameworkA = new TargetFrameworkInformation
            {
                FrameworkName = FrameworkConstants.CommonFrameworks.Net45
            };
            var frameworkB = new TargetFrameworkInformation
            {
                FrameworkName = FrameworkConstants.CommonFrameworks.NetStandard16,
                Dependencies = new List<LibraryDependency>() {
                    new LibraryDependency
                        {
                            LibraryRange = new LibraryRange
                            {
                                Name = "NuGet.Versioning",
                                VersionRange = new VersionRange(new NuGetVersion("1.0.0"))
                            }
                        }
                }
            };

            var spec = new PackageSpec(new[] { frameworkA, frameworkB });

            // Act
            PackageSpecOperations.AddOrUpdateDependency(
                spec,
                identity,
                new[] { frameworkB.FrameworkName });

            // Assert
            Assert.Empty(spec.Dependencies);
            Assert.Empty(spec.TargetFrameworks[0].Dependencies);
            Assert.Equal(identity.Id, spec.TargetFrameworks[1].Dependencies[0].LibraryRange.Name);
            Assert.Equal(
                identity.Version,
                spec.TargetFrameworks[1].Dependencies[0].LibraryRange.VersionRange.MinVersion);
        }


        [Fact]
        public void RemoveDependency_RemovesFromAllFrameworkLists()
        {
            // Arrange
            var frameworkA = new TargetFrameworkInformation
            {
                FrameworkName = FrameworkConstants.CommonFrameworks.Net45
            };
            frameworkA.Dependencies.Add(new LibraryDependency
            {
                LibraryRange = new LibraryRange
                {
                    Name = "nuget.versioning",
                    VersionRange = new VersionRange(new NuGetVersion("0.9.0"))
                }
            });
            var frameworkB = new TargetFrameworkInformation
            {
                FrameworkName = FrameworkConstants.CommonFrameworks.NetStandard16
            };
            frameworkB.Dependencies.Add(new LibraryDependency
            {
                LibraryRange = new LibraryRange
                {
                    Name = "NUGET.VERSIONING",
                    VersionRange = new VersionRange(new NuGetVersion("0.8.0"))
                }
            });
            var spec = new PackageSpec(new[] { frameworkA, frameworkB });
            spec.Dependencies.Add(new LibraryDependency
            {
                LibraryRange = new LibraryRange
                {
                    Name = "NuGet.VERSIONING",
                    VersionRange = new VersionRange(new NuGetVersion("0.7.0"))
                }
            });
            var id = "NuGet.Versioning";

            // Act
            PackageSpecOperations.RemoveDependency(spec, id);

            // Assert
            Assert.Empty(spec.Dependencies);
            Assert.Empty(spec.TargetFrameworks[0].Dependencies);
            Assert.Empty(spec.TargetFrameworks[1].Dependencies);
        }

        [Fact]
        public void HasPackage_ReturnsTrueWhenIdIsInFramework()
        {
            // Arrange
            var framework = new TargetFrameworkInformation
            {
                FrameworkName = FrameworkConstants.CommonFrameworks.Net45
            };
            framework.Dependencies.Add(new LibraryDependency
            {
                LibraryRange = new LibraryRange
                {
                    Name = "nuget.versioning",
                    VersionRange = new VersionRange(new NuGetVersion("0.9.0"))
                }
            });
            var spec = new PackageSpec(new[] { framework });
            var id = "NuGet.Versioning";

            // Act
            var actual = PackageSpecOperations.HasPackage(spec, id);

            // Assert
            Assert.True(actual);
        }

        [Fact]
        public void HasPackage_ReturnsTrueWhenIdIsForAllFrameworks()
        {
            // Arrange
            var framework = new TargetFrameworkInformation
            {
                FrameworkName = FrameworkConstants.CommonFrameworks.Net45
            };
            var spec = new PackageSpec(new[] { framework });
            spec.Dependencies.Add(new LibraryDependency
            {
                LibraryRange = new LibraryRange
                {
                    Name = "nuget.versioning",
                    VersionRange = new VersionRange(new NuGetVersion("0.9.0"))
                }
            });
            var id = "NuGet.Versioning";

            // Act
            var actual = PackageSpecOperations.HasPackage(spec, id);

            // Assert
            Assert.True(actual);
        }

        [Fact]
        public void HasPackage_ReturnsFalseWhenIdIsNotInSpec()
        {
            // Arrange
            var framework = new TargetFrameworkInformation
            {
                FrameworkName = FrameworkConstants.CommonFrameworks.Net45
            };
            var spec = new PackageSpec(new[] { framework });
            var id = "NuGet.Versioning";

            // Act
            var actual = PackageSpecOperations.HasPackage(spec, id);

            // Assert
            Assert.False(actual);
        }
    }
}
