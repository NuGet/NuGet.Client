// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using NuGet.Common;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class MSBuildProjectSystemTests
    {
        private class TestInfo : IDisposable
        {
            public MSBuildProjectSystem MSBuildProjectSystem { get; }
            private string ProjectDirectory { get; }
            private string MSBuildDirectory { get; }
            private TestNuGetProjectContext NuGetProjectContext { get; }

            public TestInfo(string projectFileContent, string projectName = "proj1")
            {
                ProjectDirectory = TestDirectory.Create();
                MSBuildDirectory = MsBuildUtility.GetMsBuildDirectory("14.0", null);
                NuGetProjectContext = new TestNuGetProjectContext();

                var projectFilePath = Path.Combine(ProjectDirectory, projectName + ".csproj");
                File.WriteAllText(projectFilePath, projectFileContent);

                MSBuildProjectSystem
                    = new MSBuildProjectSystem(MSBuildDirectory, projectFilePath, NuGetProjectContext);
            }

            public void Dispose()
            {
                TestFileSystemUtility.DeleteRandomTestFolder(ProjectDirectory);
            }
        }

        [Fact]
        public void MSBuildProjectSystem_AddFile()
        {
            // Arrange
            var projectFileContent = Util.CreateProjFileContent();
            using (var testInfo = new TestInfo(projectFileContent))
            {
                var expectedContent = "one two three";
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(expectedContent)))
                {
                    // Act
                    testInfo.MSBuildProjectSystem.AddFile("a.js", stream);

                    // Assert
                    var path = Path.Combine(testInfo.MSBuildProjectSystem.ProjectFullPath, "a.js");
                    Assert.True(File.Exists(path), path + "should exist, but it does not.");

                    var actualContent = File.ReadAllText(path);
                    Assert.Equal(expectedContent, actualContent);
                }
            }
        }

        [Fact]
        public void MSBuildProjectSystem_RemoveFile()
        {
            // Arrange
            var projectFileContent = Util.CreateProjFileContent(
                "proj1",
                "v4.5",
                references: null,
                contentFiles: new[] { "a.js" });

            using (var testInfo = new TestInfo(projectFileContent))
            {
                var path = Path.Combine(testInfo.MSBuildProjectSystem.ProjectFullPath, "a.js");
                var expectedContent = "one two three";
                File.WriteAllText(path, expectedContent);

                Assert.True(File.Exists(path), path + "should exist, but it does not.");

                // Act
                testInfo.MSBuildProjectSystem.RemoveFile("a.js");

                // Assert
                Assert.False(File.Exists(path), path + "should not exist, but it does.");
            }
        }

        [Fact]
        public void MSBuildProjectSystem_FileExistInProject()
        {
            // Arrange
            var projectFileContent = Util.CreateProjFileContent(
                "proj1",
                "v4.5",
                references: null,
                contentFiles: new[] { "a.js" });

            using (var testInfo = new TestInfo(projectFileContent))
            {
                var path = Path.Combine(testInfo.MSBuildProjectSystem.ProjectFullPath, "a.js");
                var expectedContent = "one two three";
                File.WriteAllText(path, expectedContent);

                Assert.True(File.Exists(path), path + "should exist, but it does not.");

                // Act
                var fileExistsInProject = testInfo.MSBuildProjectSystem.FileExistsInProject("a.js");

                // Assert
                Assert.True(fileExistsInProject, "a.js does not exist in project");
            }
        }
    }
}
