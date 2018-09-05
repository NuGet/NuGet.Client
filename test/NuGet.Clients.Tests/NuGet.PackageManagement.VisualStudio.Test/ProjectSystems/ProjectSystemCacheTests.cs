// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using NuGet.ProjectModel;
using NuGet.VisualStudio;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class ProjectSystemCacheTests
    {
        [Fact]
        public void TryGetVsProjectAdapter_ReturnsProjectByFullName()
        {
            // Arrange
            var target = new ProjectSystemCache();
            var projectNames = new ProjectNames(
                fullName: @"C:\src\project\project.csproj",
                uniqueName: @"folder\project",
                shortName: "project",
                customUniqueName: @"folder\project");
            var vsProjectAdapter = new Mock<IVsProjectAdapter>();

            target.AddProject(projectNames, vsProjectAdapter.Object, nuGetProject: null);
            IVsProjectAdapter actual;

            // Act
            var success = target.TryGetVsProjectAdapter(projectNames.FullName, out actual);

            // Assert
            Assert.True(success, "The project should have been fetched from the cache by full name.");
            Assert.Same(vsProjectAdapter.Object, actual);
        }

        [Fact]
        public void TryGetVsProjectAdapter_ReturnsProjectByCustomUniqueName()
        {
            // Arrange
            var target = new ProjectSystemCache();
            var projectNames = new ProjectNames(
                fullName: @"C:\src\project\project.csproj",
                uniqueName: @"folder\project",
                shortName: "project",
                customUniqueName: @"custom");
            var vsProjectAdapter = new Mock<IVsProjectAdapter>();

            target.AddProject(projectNames, vsProjectAdapter.Object, nuGetProject: null);
            IVsProjectAdapter actual;

            // Act
            var success = target.TryGetVsProjectAdapter(projectNames.CustomUniqueName, out actual);

            // Assert
            Assert.True(success, "The project should have been fetched from the cache by custom unique name.");
            Assert.Same(vsProjectAdapter.Object, actual);
        }

        [Fact]
        public void TryGetVsProjectAdapter_ReturnsProjectByUniqueName()
        {
            // Arrange
            var target = new ProjectSystemCache();
            var projectNames = new ProjectNames(
                fullName: @"C:\src\project\project.csproj",
                uniqueName: @"folder\project",
                shortName: "project",
                customUniqueName: @"folder\project");
            var vsProjectAdapter = new Mock<IVsProjectAdapter>();

            target.AddProject(projectNames, vsProjectAdapter.Object, nuGetProject: null);
            IVsProjectAdapter actual;

            // Act
            var success = target.TryGetVsProjectAdapter(projectNames.UniqueName, out actual);

            // Assert
            Assert.True(success, "The project should have been fetched from the cache by unique name.");
            Assert.Same(vsProjectAdapter.Object, actual);
        }

        [Fact]
        public void TryGetDTEProject_ReturnsProjectWhenShortNameIsNotAmbiguous()
        {
            // Arrange
            var target = new ProjectSystemCache();
            var projectNames = new ProjectNames(
                fullName: @"C:\src\project\project.csproj",
                uniqueName: @"folder\project",
                shortName: "project",
                customUniqueName: @"folder\project");
            var vsProjectAdapter = new Mock<IVsProjectAdapter>();

            target.AddProject(projectNames, vsProjectAdapter.Object, nuGetProject: null);
            IVsProjectAdapter actual;

            // Act
            var success = target.TryGetVsProjectAdapter(projectNames.ShortName, out actual);

            // Assert
            Assert.True(success, "The project should have been fetched from the cache by short name.");
            Assert.Same(vsProjectAdapter.Object, actual);
        }

        [Fact]
        public void TryGetDTEProject_ReturnsNullWhenShortNameIsAmbiguous()
        {
            // Arrange
            var target = new ProjectSystemCache();

            var projectNamesA = new ProjectNames(
                fullName: @"C:\src\projectA\project.csproj",
                uniqueName: @"folderA\project",
                shortName: "project",
                customUniqueName: @"folderA\project");

            var projectNamesB = new ProjectNames(
                fullName: @"C:\src\projectB\project.csproj",
                uniqueName: @"folderB\project",
                shortName: projectNamesA.ShortName,
                customUniqueName: @"folderB\project");
            
            target.AddProject(projectNamesA, vsProjectAdapter: null, nuGetProject: null);
            target.AddProject(projectNamesB, vsProjectAdapter: null, nuGetProject: null);

            IVsProjectAdapter actual;

            // Act
            var success = target.TryGetVsProjectAdapter(projectNamesA.ShortName, out actual);

            // Assert
            Assert.False(success, "The project should not have been fetched from the cache by short name.");
            Assert.Null(actual);
        }

        [Fact]
        public void AddProjectRestoreInfo_AfterAddProject_UpdatesCacheEntry()
        {
            // Arrange
            var target = new ProjectSystemCache();
            var projectNames = new ProjectNames(
                fullName: @"C:\src\project\project.csproj",
                uniqueName: @"folder\project",
                shortName: "project",
                customUniqueName: @"folder\project");
            var projectNamesFromFullPath = ProjectNames.FromFullProjectPath(@"C:\src\project\project.csproj");
            var projectRestoreInfo = new DependencyGraphSpec();

            target.AddProject(projectNames, vsProjectAdapter: null, nuGetProject: null);

            // Act
            target.AddProjectRestoreInfo(projectNamesFromFullPath, projectRestoreInfo);

            // Assert
            var getPackageSpecSuccess = target.TryGetProjectRestoreInfo(projectNames.FullName, out var actual);
            var getProjectNameFromUniqueNameSuccess = target.TryGetProjectNames(projectNames.UniqueName, out var names1);
            var getProjectNameFromFullNameSuccess = target.TryGetProjectNames(projectNames.FullName, out var names2);

            Assert.True(getPackageSpecSuccess);
            Assert.True(getProjectNameFromUniqueNameSuccess);
            Assert.True(getProjectNameFromFullNameSuccess);
            Assert.Same(projectRestoreInfo, actual);
            Assert.Equal(@"folder\project", names1.CustomUniqueName);
            Assert.Equal(@"folder\project", names2.CustomUniqueName);
        }

        [Fact]
        public void AddProject_AfterAddProjectRestoreInfo_UpdatesCacheEntry()
        {
            // Arrange
            var target = new ProjectSystemCache();
            var projectNames = new ProjectNames(
                fullName: @"C:\src\project\project.csproj",
                uniqueName: @"folder\project",
                shortName: "project",
                customUniqueName: @"folder\project");
            var projectNamesFromFullPath = ProjectNames.FromFullProjectPath(@"C:\src\project\project.csproj");
            var projectRestoreInfo = new DependencyGraphSpec();

            target.AddProjectRestoreInfo(projectNamesFromFullPath, projectRestoreInfo);

            // Act
            target.AddProject(projectNames, vsProjectAdapter: null, nuGetProject: null);

            // Assert
            DependencyGraphSpec actual;
            ProjectNames names;

            var getPackageSpecSuccess = target.TryGetProjectRestoreInfo(projectNames.FullName, out actual);
            var getProjectNameSuccess = target.TryGetProjectNames(projectNames.UniqueName, out names);

            Assert.True(getPackageSpecSuccess);
            Assert.True(getProjectNameSuccess);
            Assert.Same(projectRestoreInfo, actual);
            Assert.Equal(@"folder\project", names.CustomUniqueName);
        }

        [Fact]
        public void AddProjectRestoreInfo_TriggersNoEvent_NoEventHandler()
        {
            // Arrange
            var target = new ProjectSystemCache();
            var projectNames = new ProjectNames(
                fullName: @"C:\src\project\project.csproj",
                uniqueName: @"folder\project",
                shortName: "project",
                customUniqueName: @"folder\project");
            var projectNamesFromFullPath = ProjectNames.FromFullProjectPath(@"C:\src\project\project.csproj");
            var projectRestoreInfo = new DependencyGraphSpec();

            // Act
            target.AddProjectRestoreInfo(projectNamesFromFullPath, projectRestoreInfo);
            target.AddProject(projectNames, vsProjectAdapter: null, nuGetProject: null);

            // Assert
            DependencyGraphSpec actual;
            ProjectNames names;
            var getPackageSpecSuccess = target.TryGetProjectRestoreInfo(projectNames.FullName, out actual);
            var getProjectNameSuccess = target.TryGetProjectNames(projectNames.UniqueName, out names);

            Assert.True(getPackageSpecSuccess);
            Assert.True(getProjectNameSuccess);
            Assert.Same(projectRestoreInfo, actual);
            Assert.Equal(@"folder\project", names.CustomUniqueName);
            // Cache remains clean since no one is listening to the cache events
            Assert.Equal(target.IsCacheDirty, 0);
        }

        [Fact]
        public void AddProjectRestoreInfo_TriggersEvent_WithEventHandler_WithReset()
        {
            // Arrange
            var target = new ProjectSystemCache();
            var projectNames = new ProjectNames(
                fullName: @"C:\src\project\project.csproj",
                uniqueName: @"folder\project",
                shortName: "project",
                customUniqueName: @"folder\project");
            var projectNamesFromFullPath = ProjectNames.FromFullProjectPath(@"C:\src\project\project.csproj");
            var projectRestoreInfo = new DependencyGraphSpec();
            var eventCount = 0;
            target.CacheUpdated += delegate (object sender, NuGetEventArgs<string> e)
            {
                if (target.TestResetDirtyFlag())
                {
                    eventCount++;
                }
            };

            // Act
            target.AddProjectRestoreInfo(projectNamesFromFullPath, projectRestoreInfo);            
            target.AddProject(projectNames, vsProjectAdapter: null, nuGetProject: null);

            // Assert
            DependencyGraphSpec actual;
            ProjectNames names;
            var getPackageSpecSuccess = target.TryGetProjectRestoreInfo(projectNames.FullName, out actual);
            var getProjectNameSuccess = target.TryGetProjectNames(projectNames.UniqueName, out names);

            Assert.True(getPackageSpecSuccess);
            Assert.True(getProjectNameSuccess);
            Assert.Same(projectRestoreInfo, actual);
            Assert.Equal(@"folder\project", names.CustomUniqueName);
            Assert.Equal(target.IsCacheDirty, 0);
            Assert.Equal(eventCount, 1);
        }

        [Fact]
        public void AddProjectRestoreInfo_TriggersEvent_WithEventHandler_NoReset()
        {
            // Arrange
            var target = new ProjectSystemCache();
            var projectNames = new ProjectNames(
                fullName: @"C:\src\project\project.csproj",
                uniqueName: @"folder\project",
                shortName: "project",
                customUniqueName: @"folder\project");
            var projectNamesFromFullPath = ProjectNames.FromFullProjectPath(@"C:\src\project\project.csproj");
            var projectRestoreInfo = new DependencyGraphSpec();
            var eventCount = 0;
            target.CacheUpdated += delegate (object sender, NuGetEventArgs<string> e)
            {
                eventCount++;
            };

            // Act
            target.AddProjectRestoreInfo(projectNamesFromFullPath, projectRestoreInfo);
            target.AddProjectRestoreInfo(projectNamesFromFullPath, projectRestoreInfo);
            target.AddProject(projectNames, vsProjectAdapter: null, nuGetProject: null);

            // Assert
            DependencyGraphSpec actual;
            ProjectNames names;
            var getPackageSpecSuccess = target.TryGetProjectRestoreInfo(projectNames.FullName, out actual);
            var getProjectNameSuccess = target.TryGetProjectNames(projectNames.UniqueName, out names);

            Assert.True(getPackageSpecSuccess);
            Assert.True(getProjectNameSuccess);
            Assert.Same(projectRestoreInfo, actual);
            Assert.Equal(@"folder\project", names.CustomUniqueName);

            // Since no listener resets the dirty flag, the cache remains dirty and only 1 event is raised.
            Assert.Equal(target.IsCacheDirty, 1);
            Assert.Equal(eventCount, 1);
        }

        [Fact]
        public void AddProjectRestoreInfo_TriggersMultipleEvent_WithEventHandler_WithReset()
        {
            // Arrange
            var target = new ProjectSystemCache();
            var projectNames = new ProjectNames(
                fullName: @"C:\src\project\project.csproj",
                uniqueName: @"folder\project",
                shortName: "project",
                customUniqueName: @"folder\project");
            var projectNamesFromFullPath = ProjectNames.FromFullProjectPath(@"C:\src\project\project.csproj");
            var projectRestoreInfo = new DependencyGraphSpec();
            var eventCount = 0;
            target.CacheUpdated += delegate (object sender, NuGetEventArgs<string> e)
            {
                if (target.TestResetDirtyFlag())
                {
                    eventCount++;
                }
            };

            // Act
            target.AddProjectRestoreInfo(projectNamesFromFullPath, projectRestoreInfo);
            target.AddProjectRestoreInfo(projectNamesFromFullPath, projectRestoreInfo);
            target.AddProjectRestoreInfo(projectNamesFromFullPath, projectRestoreInfo);
            target.AddProjectRestoreInfo(projectNamesFromFullPath, projectRestoreInfo);

            // Assert
            Assert.Equal(target.IsCacheDirty, 0);
            Assert.Equal(eventCount, 4);
        }

        [Fact]
        public void AddProject_RemoveProject_Clear_TriggerNoEvent_WithEventHandler()
        {
            // Arrange
            var target = new ProjectSystemCache();
            var projectNames = new ProjectNames(
                fullName: @"C:\src\project\project.csproj",
                uniqueName: @"folder\project",
                shortName: "project",
                customUniqueName: @"folder\project");
            var projectNamesFromFullPath = ProjectNames.FromFullProjectPath(@"C:\src\project\project.csproj");
            var projectRestoreInfo = new DependencyGraphSpec();
            var eventCount = 0;
            target.CacheUpdated += delegate (object sender, NuGetEventArgs<string> e)
            {
                if (target.TestResetDirtyFlag())
                {
                    eventCount++;
                }
            };

            // Act
            target.AddProject(projectNames, vsProjectAdapter: null, nuGetProject: null);
            target.RemoveProject(projectNames.FullName);
            target.Clear();

            // Assert
            Assert.Equal(target.IsCacheDirty, 0);
            Assert.Equal(eventCount, 0);
        }
    }
}
