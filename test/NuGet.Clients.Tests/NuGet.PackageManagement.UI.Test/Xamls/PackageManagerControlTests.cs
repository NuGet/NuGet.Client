// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Moq;
using NuGet.VisualStudio.Internal.Contracts;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    public class PackageManagerControlTests
    {
        [Theory]
        [InlineData(true, NuGetProjectKind.PackagesConfig)]
        [InlineData(true, NuGetProjectKind.PackageReference, NuGetProjectKind.PackagesConfig)]
        [InlineData(false, NuGetProjectKind.PackageReference)]
        [InlineData(false)]
        public void ShouldRefreshOnVisibleChange_WithoutPendingRefresh_RefreshesOnlyPackagesConfigProjects(
            bool expected,
            params NuGetProjectKind[] projectKinds)
        {
            IProjectContextInfo[] projects = projectKinds
                .Select(kind => Mock.Of<IProjectContextInfo>(project => project.ProjectKind == kind))
                .ToArray();

            bool actual = PackageManagerControl.ShouldRefreshOnVisibleChange(
                isVisible: true,
                loadedAndInitialized: true,
                refreshOnVisibleChange: false,
                projects: projects);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(NuGetProjectKind.PackageReference)]
        [InlineData(NuGetProjectKind.PackagesConfig)]
        [InlineData]
        public void ShouldRefreshOnVisibleChange_WithPendingRefresh_Refreshes(
            params NuGetProjectKind[] projectKinds)
        {
            IProjectContextInfo[] projects = projectKinds
                .Select(kind => Mock.Of<IProjectContextInfo>(project => project.ProjectKind == kind))
                .ToArray();

            bool actual = PackageManagerControl.ShouldRefreshOnVisibleChange(
                isVisible: true,
                loadedAndInitialized: true,
                refreshOnVisibleChange: true,
                projects: projects);

            Assert.True(actual);
        }

        [Theory]
        [InlineData(false, true, false)]
        [InlineData(false, true, true)]
        [InlineData(true, false, false)]
        [InlineData(true, false, true)]
        [InlineData(false, false, false)]
        [InlineData(false, false, true)]
        public void ShouldRefreshOnVisibleChange_WhenHiddenOrNotInitialized_DoesNotRefresh(
            bool isVisible,
            bool loadedAndInitialized,
            bool refreshOnVisibleChange)
        {
            var projects = new[]
            {
                Mock.Of<IProjectContextInfo>(project => project.ProjectKind == NuGetProjectKind.PackagesConfig)
            };

            bool actual = PackageManagerControl.ShouldRefreshOnVisibleChange(
                isVisible,
                loadedAndInitialized,
                refreshOnVisibleChange,
                projects);

            Assert.False(actual);
        }

        [Fact]
        public void ShouldRefreshOnVisibleChange_BeforeInitialization_DoesNotRequireProjects()
        {
            bool actual = PackageManagerControl.ShouldRefreshOnVisibleChange(
                isVisible: true,
                loadedAndInitialized: false,
                refreshOnVisibleChange: false,
                projects: null!);

            Assert.False(actual);
        }
    }
}
