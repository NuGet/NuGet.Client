// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using NuGet.Configuration;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Build.Tasks.Test
{
    public class GetRestoreProjectStyleTaskTests
    {
        [Fact]
        public void GetRestoreProjectStyleTask_CanDetectPackageReference()
        {
            var buildEngine = new TestBuildEngine();

            var task = new GetRestoreProjectStyleTask
            {
                BuildEngine = buildEngine,
                HasPackageReferenceItems = true
            };

            task.Execute().Should().BeTrue();

            task.ProjectStyle.Should().Be(ProjectStyle.PackageReference);
            task.PackageReferenceCompatibleProjectStyle.Should().BeTrue();
        }

        [Theory]
        [InlineData("packages.config")]
        [InlineData("packages.ProjectA.config")]
        public void GetRestoreProjectStyleTask_CanDetectPackagesConfig(string packagesConfigFileName)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                File.WriteAllText(Path.Combine(testDirectory, packagesConfigFileName), string.Empty);

                var buildEngine = new TestBuildEngine();

                var task = new GetRestoreProjectStyleTask
                {
                    BuildEngine = buildEngine,
                    MSBuildProjectDirectory = testDirectory,
                    MSBuildProjectName = "ProjectA"
                };

                task.Execute().Should().BeTrue();

                task.ProjectStyle.Should().Be(ProjectStyle.PackagesConfig);
                task.PackageReferenceCompatibleProjectStyle.Should().BeFalse();
            }
        }

        [Fact]
        public void GetRestoreProjectStyleTask_CanDetectProjectJson()
        {
            var buildEngine = new TestBuildEngine();

            var task = new GetRestoreProjectStyleTask
            {
                BuildEngine = buildEngine,
                ProjectJsonPath = "SomePath"
            };

            task.Execute().Should().BeTrue();

            task.ProjectStyle.Should().Be(ProjectStyle.ProjectJson);
            task.PackageReferenceCompatibleProjectStyle.Should().BeFalse();
        }

        [Fact]
        public void GetRestoreProjectStyleTask_CanParseExistingProjectStyle()
        {
            foreach (var lowerCase in new [] { true, false })
            {
                foreach (var projectStyle in Enum.GetValues(typeof(ProjectStyle)).Cast<ProjectStyle>())
                {
                    var buildEngine = new TestBuildEngine();

                    var task = new GetRestoreProjectStyleTask
                    {
                        BuildEngine = buildEngine,
                        RestoreProjectStyle = lowerCase ? projectStyle.ToString().ToLower() : projectStyle.ToString()
                    };

                    task.Execute().Should().BeTrue();

                    task.ProjectStyle.Should().Be(projectStyle);
                    if (projectStyle == ProjectStyle.PackageReference || projectStyle == ProjectStyle.DotnetToolReference)
                    {
                        task.PackageReferenceCompatibleProjectStyle.Should().BeTrue();
                    }
                    else
                    {
                        task.PackageReferenceCompatibleProjectStyle.Should().BeFalse();
                    }
                }
            }
            
        }

        [Fact]
        public void GetRestoreProjectStyleTask_DefaultsToUnknown()
        {
            var buildEngine = new TestBuildEngine();

            var task = new GetRestoreProjectStyleTask
            {
                BuildEngine = buildEngine,
                RestoreProjectStyle = string.Empty,
                ProjectJsonPath = string.Empty,
                HasPackageReferenceItems = false,
                MSBuildProjectName = "ProjectA",
                MSBuildProjectDirectory = "SomeDirectory"
            };

            task.Execute().Should().BeTrue();

            task.ProjectStyle.Should().Be(ProjectStyle.Unknown);
            task.PackageReferenceCompatibleProjectStyle.Should().BeFalse();
        }

        [Fact]
        public void GetRestoreProjectStyleTask_LogsErrorWhenInvalidRestoreStyleSpecified()
        {
            var buildEngine = new TestBuildEngine();
            var testLogger = buildEngine.TestLogger;

            var task = new GetRestoreProjectStyleTask
            {
                BuildEngine = buildEngine,
                RestoreProjectStyle = "Invalid"
            };

            task.Execute().Should().BeFalse();

            testLogger.ErrorMessages.Should().ContainSingle().Which.Should().Be("Invalid project restore style 'Invalid'.");
        }

        [Fact]
        public void GetRestoreProjectStyleTask_UserSpecifiedValueOverridesDetectedValue()
        {
            var expected = ProjectStyle.Standalone;

            using (var testDirectory = TestDirectory.Create())
            {
                File.WriteAllText(Path.Combine(testDirectory, NuGetConstants.PackageReferenceFile), string.Empty);

                var buildEngine = new TestBuildEngine();

                var task = new GetRestoreProjectStyleTask
                {
                    BuildEngine = buildEngine,
                    RestoreProjectStyle = expected.ToString(),
                    ProjectJsonPath = "Some value",
                    HasPackageReferenceItems = true,
                    MSBuildProjectName = "ProjectA",
                    MSBuildProjectDirectory = "SomeDirectory"
                };

                task.Execute().Should().BeTrue();

                task.ProjectStyle.Should().Be(expected);
                task.PackageReferenceCompatibleProjectStyle.Should().BeFalse();
            }
        }
    }
}
