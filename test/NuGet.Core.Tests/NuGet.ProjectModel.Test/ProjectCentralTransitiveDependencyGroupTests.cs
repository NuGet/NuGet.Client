// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Versioning;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class ProjectCentralTransitiveDependencyGroupTests
    {
        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void ProjectCentralTransitiveDependencyGroup_ConstructorNullArgumentCheck(bool nullFramework, bool nullDependencies)
        {
            // Arrange
            var nuGetFramework = nullFramework ? null : NuGetFramework.Parse("NETStandard2.0");
            var dependencies = nullDependencies ? null : Enumerable.Empty<LibraryDependency>();

            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => new ProjectModel.ProjectCentralTransitiveDependencyGroup(nuGetFramework, dependencies));
        }

        [Fact]
        public void ProjectCentralTransitiveDependencyGroup_GetProperties()
        {
            // Arrange
            var nuGetFramework = NuGetFramework.Parse("NETStandard2.0");
            var libraryDep = new LibraryDependency
            {
                LibraryRange = new LibraryRange(
                                "packageA",
                                VersionRange.Parse("1.0.0"),
                                LibraryDependencyTarget.Package)
            };
            var dependencies = new List<LibraryDependency>() { libraryDep };
            var projectCentralTransitiveDependencyGroup = new ProjectModel.ProjectCentralTransitiveDependencyGroup(nuGetFramework, dependencies);

            // Act
            var framework = projectCentralTransitiveDependencyGroup.FrameworkName;
            var tDependencies = projectCentralTransitiveDependencyGroup.TransitiveDependencies;

            // Assert
            Assert.Equal(dependencies, tDependencies);
            Assert.Equal(nuGetFramework.GetShortFolderName(), framework);
        }

        [Fact]
        public void ProjectCentralTransitiveDependencyGroup_EqualObjects()
        {
            // Arrange
            var nuGetFramework = NuGetFramework.Parse("NETStandard2.0");
            var libraryDep = new LibraryDependency
            {
                LibraryRange = new LibraryRange(
                                "packageA",
                                VersionRange.Parse("1.0.0"),
                                LibraryDependencyTarget.Package)
            };
            var dependencies = new List<LibraryDependency>() { libraryDep };
            var projectCentralTransitiveDependencyGroup1 = new ProjectModel.ProjectCentralTransitiveDependencyGroup(nuGetFramework, dependencies);
            var projectCentralTransitiveDependencyGroup2 = new ProjectModel.ProjectCentralTransitiveDependencyGroup(nuGetFramework, dependencies);

            // Act = Assert
            Assert.True(projectCentralTransitiveDependencyGroup1.Equals(projectCentralTransitiveDependencyGroup1));
            Assert.True(projectCentralTransitiveDependencyGroup1.Equals(projectCentralTransitiveDependencyGroup2));
            Assert.Equal(projectCentralTransitiveDependencyGroup1.GetHashCode(), projectCentralTransitiveDependencyGroup2.GetHashCode());
        }

        [Fact]
        public void ProjectCentralTransitiveDependencyGroup_NotEqualObjects()
        {
            // Arrange
            var nuGetFramework1 = NuGetFramework.Parse("NETStandard2.0");
            var nuGetFramework2 = NuGetFramework.Parse("NETStandard3.0");
            var libraryDep1 = new LibraryDependency
            {
                LibraryRange = new LibraryRange(
                                "packageA",
                                VersionRange.Parse("1.0.0"),
                                LibraryDependencyTarget.Package)
            };
            var libraryDep2 = new LibraryDependency
            {
                LibraryRange = new LibraryRange(
                                "packageB",
                                VersionRange.Parse("1.0.0"),
                                LibraryDependencyTarget.Package)
            };
            var dependencies1 = new List<LibraryDependency>() { libraryDep1 };
            var dependencies2 = new List<LibraryDependency>() { libraryDep2 };

            var projectCentralTransitiveDependencyGroup11 = new ProjectModel.ProjectCentralTransitiveDependencyGroup(nuGetFramework1, dependencies1);
            var projectCentralTransitiveDependencyGroup12 = new ProjectModel.ProjectCentralTransitiveDependencyGroup(nuGetFramework1, dependencies2);
            var projectCentralTransitiveDependencyGroup21 = new ProjectModel.ProjectCentralTransitiveDependencyGroup(nuGetFramework2, dependencies1);
            var projectCentralTransitiveDependencyGroup22 = new ProjectModel.ProjectCentralTransitiveDependencyGroup(nuGetFramework2, dependencies2);

            // Act = Assert
            Assert.False(projectCentralTransitiveDependencyGroup11.Equals(null));
            Assert.False(projectCentralTransitiveDependencyGroup11.Equals(projectCentralTransitiveDependencyGroup12));
            Assert.False(projectCentralTransitiveDependencyGroup11.Equals(projectCentralTransitiveDependencyGroup21));
            Assert.False(projectCentralTransitiveDependencyGroup11.Equals(projectCentralTransitiveDependencyGroup22));

            Assert.False(projectCentralTransitiveDependencyGroup12.Equals(projectCentralTransitiveDependencyGroup21));
            Assert.False(projectCentralTransitiveDependencyGroup12.Equals(projectCentralTransitiveDependencyGroup22));

            Assert.False(projectCentralTransitiveDependencyGroup21.Equals(projectCentralTransitiveDependencyGroup22));
        }
    }
}
