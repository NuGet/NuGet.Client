// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Locator;
using NuGet.CommandLine.XPlat;
using NuGet.CommandLine.Xplat.Tests.Utility;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit;
using System.Globalization;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class XPlatWhyUnitTests
    {
        static XPlatWhyUnitTests()
        {
            MSBuildLocator.RegisterDefaults();
        }

        [Fact]
        public void WhyCommand_DependencyGraphFinder_MultipleDependencyPathsForTargetPackage_AllPathsFound()
        {
            // Arrange
            var lockFileFormat = new LockFileFormat();
            var lockFileContent = ProtocolUtility.GetResource("NuGet.CommandLine.Xplat.Tests.compiler.resources.DNW.Test.SampleProject1.project.assets.json", GetType());
            var assetsFile = lockFileFormat.Parse(lockFileContent, "In Memory");

            if (XunitAttributeUtility.CurrentPlatform == Platform.Linux || XunitAttributeUtility.CurrentPlatform == Platform.Darwin)
            {
                ConvertRelevantWindowsPathsToUnix(assetsFile);
            }

            string targetPackage = "System.Text.Json";
            var frameworks = new List<string>();

            // Act
            var dependencyGraphs = DependencyGraphFinder.GetAllDependencyGraphsForTarget(assetsFile, targetPackage, frameworks);

            // Assert

            // direct dependency on target package is found
            Assert.Contains(dependencyGraphs["net472"], dep => (dep.Id == "System.Text.Json") && (dep.Version == "8.0.0"));

            // transitive dependency from a top-level package is found, with the correct resolved version
            Assert.Contains(dependencyGraphs["net472"].First(dep => dep.Id == "Azure.Core").Children, dep => (dep.Id == "System.Text.Json") && (dep.Version == "8.0.0"));

            // transitive dependency from a top-level project reference is found, with the correct resolved version
            Assert.Contains(dependencyGraphs["net472"].First(dep => dep.Id == "DotnetNuGetWhyPackage").Children, dep => (dep.Id == "System.Text.Json") && (dep.Version == "8.0.0"));
        }

        [Fact]
        public void WhyCommand_DependencyGraphFinder_DependencyOnTargetProject_AllPathsFound()
        {
            // Arrange
            var lockFileFormat = new LockFileFormat();
            var lockFileContent = ProtocolUtility.GetResource("NuGet.CommandLine.Xplat.Tests.compiler.resources.DNW.Test.SampleProject1.project.assets.json", GetType());
            var assetsFile = lockFileFormat.Parse(lockFileContent, "In Memory");

            if (XunitAttributeUtility.CurrentPlatform == Platform.Linux || XunitAttributeUtility.CurrentPlatform == Platform.Darwin)
            {
                ConvertRelevantWindowsPathsToUnix(assetsFile);
            }

            string targetPackage = "DotnetNuGetWhyPackage"; // project reference
            var frameworks = new List<string>();

            // Act
            var dependencyGraphs = DependencyGraphFinder.GetAllDependencyGraphsForTarget(assetsFile, targetPackage, frameworks);

            // Assert

            // direct dependency on target project reference is found
            Assert.Contains(dependencyGraphs["net472"], dep => dep.Id == "DotnetNuGetWhyPackage");

            // transitive dependency on target project reference is found
            Assert.Contains(dependencyGraphs["net472"].First(dep => dep.Id == "CustomProjectName").Children, dep => dep.Id == "DotnetNuGetWhyPackage");
        }

        [Fact]
        public void WhyCommand_DependencyGraphFinder_NoDependencyOnTargetPackage_ReturnsNullGraph()
        {
            // Arrange
            var lockFileFormat = new LockFileFormat();
            var lockFileContent = ProtocolUtility.GetResource("NuGet.CommandLine.Xplat.Tests.compiler.resources.DNW.Test.SampleProject1.project.assets.json", GetType());
            var assetsFile = lockFileFormat.Parse(lockFileContent, "In Memory");

            if (XunitAttributeUtility.CurrentPlatform == Platform.Linux || XunitAttributeUtility.CurrentPlatform == Platform.Darwin)
            {
                ConvertRelevantWindowsPathsToUnix(assetsFile);
            }

            string targetPackage = "NotARealPackage";
            var frameworks = new List<string>();

            // Act
            var dependencyGraphs = DependencyGraphFinder.GetAllDependencyGraphsForTarget(assetsFile, targetPackage, frameworks);

            // Assert

            // no paths found for any framework
            Assert.Null(dependencyGraphs);
        }

        [Fact]
        public void WhyCommand_DependencyGraphFinder_DependencyOnTargetPackageForOnlyOneFramework_ReturnsCorrectGraphs()
        {
            // Arrange
            var lockFileFormat = new LockFileFormat();
            var lockFileContent = ProtocolUtility.GetResource("NuGet.CommandLine.Xplat.Tests.compiler.resources.DNW.Test.SampleProject1.project.assets.json", GetType());
            var assetsFile = lockFileFormat.Parse(lockFileContent, "In Memory");

            if (XunitAttributeUtility.CurrentPlatform == Platform.Linux || XunitAttributeUtility.CurrentPlatform == Platform.Darwin)
            {
                ConvertRelevantWindowsPathsToUnix(assetsFile);
            }

            string targetPackage = "Azure.Core";
            var frameworks = new List<string>();

            // Act
            var dependencyGraphs = DependencyGraphFinder.GetAllDependencyGraphsForTarget(assetsFile, targetPackage, frameworks);

            // Assert

            // paths found for one framework
            Assert.Contains(dependencyGraphs["net472"], dep => dep.Id == "Azure.Core");

            // no paths found for the other framework
            Assert.Null(dependencyGraphs["net6.0"]);
        }

        [Fact]
        public void WhyCommand_DependencyGraphFinder_DependencyOnTargetPackage_FrameworkOptionSpecified_PathIsFound()
        {
            // Arrange
            var lockFileFormat = new LockFileFormat();
            var lockFileContent = ProtocolUtility.GetResource("NuGet.CommandLine.Xplat.Tests.compiler.resources.DNW.Test.SampleProject1.project.assets.json", GetType());
            var assetsFile = lockFileFormat.Parse(lockFileContent, "In Memory");

            if (XunitAttributeUtility.CurrentPlatform == Platform.Linux || XunitAttributeUtility.CurrentPlatform == Platform.Darwin)
            {
                ConvertRelevantWindowsPathsToUnix(assetsFile);
            }

            string targetPackage = "Azure.Core";
            List<string> frameworks = ["net472"];

            // Act
            var dependencyGraphs = DependencyGraphFinder.GetAllDependencyGraphsForTarget(assetsFile, targetPackage, frameworks);

            // Assert

            // Path found
            Assert.Contains(dependencyGraphs["net472"], dep => dep.Id == "Azure.Core");
        }

        [Fact]
        public void WhyCommand_DependencyGraphFinder_DependencyOnTargetPackageForOnlyOneFramework_DifferentFrameworkSpecified_ReturnsNullGraph()
        {
            // Arrange
            var lockFileFormat = new LockFileFormat();
            var lockFileContent = ProtocolUtility.GetResource("NuGet.CommandLine.Xplat.Tests.compiler.resources.DNW.Test.SampleProject1.project.assets.json", GetType());
            var assetsFile = lockFileFormat.Parse(lockFileContent, "In Memory");

            if (XunitAttributeUtility.CurrentPlatform == Platform.Linux || XunitAttributeUtility.CurrentPlatform == Platform.Darwin)
            {
                ConvertRelevantWindowsPathsToUnix(assetsFile);
            }

            string targetPackage = "Azure.Core";
            List<string> frameworks = ["net6.0"];

            // Act
            var dependencyGraphs = DependencyGraphFinder.GetAllDependencyGraphsForTarget(assetsFile, targetPackage, frameworks);

            // Assert

            // no paths found
            Assert.Null(dependencyGraphs);
        }

        [Fact]
        public void WhyCommand_DependencyGraphFinder_DifferentCaseUsedForTargetPackageId_MatchesAreCaseInsensitive_AllPathsFound()
        {
            // Arrange
            var lockFileFormat = new LockFileFormat();
            var lockFileContent = ProtocolUtility.GetResource("NuGet.CommandLine.Xplat.Tests.compiler.resources.DNW.Test.SampleProject1.project.assets.json", GetType());
            var assetsFile = lockFileFormat.Parse(lockFileContent, "In Memory");

            if (XunitAttributeUtility.CurrentPlatform == Platform.Linux || XunitAttributeUtility.CurrentPlatform == Platform.Darwin)
            {
                ConvertRelevantWindowsPathsToUnix(assetsFile);
            }

            string targetPackage = "system.text.jsoN";
            var frameworks = new List<string>();

            // Act
            var dependencyGraphs = DependencyGraphFinder.GetAllDependencyGraphsForTarget(assetsFile, targetPackage, frameworks);

            // Assert

            // direct dependency on target package is found
            Assert.Contains(dependencyGraphs["net472"], dep => (dep.Id == "System.Text.Json") && (dep.Version == "8.0.0"));

            // transitive dependency from a top-level package is found, with the correct resolved version
            Assert.Contains(dependencyGraphs["net472"].First(dep => dep.Id == "Azure.Core").Children, dep => (dep.Id == "System.Text.Json") && (dep.Version == "8.0.0"));

            // transitive dependency from a top-level project reference is found, with the correct resolved version
            Assert.Contains(dependencyGraphs["net472"].First(dep => dep.Id == "DotnetNuGetWhyPackage").Children, dep => (dep.Id == "System.Text.Json") && (dep.Version == "8.0.0"));
        }

        [Fact]
        public void WhyCommand_GetListOfProjectPaths_WithProjectFile_ReturnsCorrectPaths()
        {
            // Arrange
            var pathContext = new SimpleTestPathContext();
            var testLogger = new XPlatTestLogger();

            var net8 = NuGetFramework.Parse("net8.0");

            var projectA = SimpleTestProjectContext.CreateNETCore(
                "a",
                pathContext.SolutionRoot,
                net8);

            projectA.Save();

            var whyCommandArgs = new WhyCommandArgs(
                path: projectA.ProjectPath,
                package: "package",
                frameworks: new List<string>(),
                logger: testLogger);

            // Act
            var projectList = whyCommandArgs.GetListOfProjectPaths();

            // Assert
            Assert.NotNull(projectList);
            Assert.Equal(projectList.Count(), 1);

            Assert.Contains(projectA.ProjectPath, projectList);
        }

        [Fact]
        public void WhyCommand_GetListOfProjectPaths_WithProjectDirectory_ReturnsCorrectPaths()
        {
            // Arrange
            var pathContext = new SimpleTestPathContext();
            var testLogger = new XPlatTestLogger();

            var net8 = NuGetFramework.Parse("net8.0");

            var projectA = SimpleTestProjectContext.CreateNETCore(
                "a",
                pathContext.SolutionRoot,
                net8);

            projectA.Save();

            var whyCommandArgs = new WhyCommandArgs(
                path: Path.GetDirectoryName(projectA.ProjectPath),
                package: "package",
                frameworks: new List<string>(),
                logger: testLogger);

            // Act
            var projectList = whyCommandArgs.GetListOfProjectPaths();

            // Assert
            Assert.NotNull(projectList);
            Assert.Equal(projectList.Count(), 1);

            Assert.Contains(projectA.ProjectPath, projectList);
        }

        [Fact]
        public void WhyCommand_GetListOfProjectPaths_WithSolutionFile_ReturnsCorrectPaths()
        {
            // Arrange
            var pathContext = new SimpleTestPathContext();
            var testLogger = new XPlatTestLogger();

            var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

            var net8 = NuGetFramework.Parse("net8.0");

            var projectA = SimpleTestProjectContext.CreateNETCore(
                "a",
                pathContext.SolutionRoot,
                net8);
            var projectB = SimpleTestProjectContext.CreateNETCore(
                "b",
                pathContext.SolutionRoot,
                net8);

            solution.Projects.Add(projectA);
            solution.Projects.Add(projectB);
            solution.Create(pathContext.SolutionRoot);

            var whyCommandArgs = new WhyCommandArgs(
                path: solution.SolutionPath,
                package: "package",
                frameworks: new List<string>(),
                logger: testLogger);

            // Act
            var projectList = whyCommandArgs.GetListOfProjectPaths();

            // Assert
            Assert.NotNull(projectList);
            Assert.Equal(projectList.Count(), 2);

            Assert.Contains(projectA.ProjectPath, projectList);
            Assert.Contains(projectB.ProjectPath, projectList);
        }

        [Fact]
        public void WhyCommand_GetListOfProjectPaths_WithSolutionDirectory_ReturnsCorrectPaths()
        {
            // Arrange
            var pathContext = new SimpleTestPathContext();
            var testLogger = new XPlatTestLogger();

            var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

            var net8 = NuGetFramework.Parse("net8.0");

            var projectA = SimpleTestProjectContext.CreateNETCore(
                "a",
                pathContext.SolutionRoot,
                net8);
            var projectB = SimpleTestProjectContext.CreateNETCore(
                "b",
                pathContext.SolutionRoot,
                net8);

            solution.Projects.Add(projectA);
            solution.Projects.Add(projectB);
            solution.Create(pathContext.SolutionRoot);

            var whyCommandArgs = new WhyCommandArgs(
                path: pathContext.SolutionRoot,
                package: "package",
                frameworks: new List<string>(),
                logger: testLogger);

            // Act
            var projectList = whyCommandArgs.GetListOfProjectPaths();

            // Assert
            Assert.NotNull(projectList);
            Assert.Equal(projectList.Count(), 2);

            Assert.Contains(projectA.ProjectPath, projectList);
            Assert.Contains(projectB.ProjectPath, projectList);
        }

        [Theory]
        [InlineData("X.sln", "Y.sln")]
        [InlineData("A.csproj", "B.csproj")]
        [InlineData("X.sln", "A.csproj")]
        public void WhyCommand_GetListOfProjectPaths_WithDirectoryWithMultipleSolutionsOrProjects_ReturnsNull(params string[] directoryFiles)
        {
            // Arrange
            var pathContext = new SimpleTestPathContext();
            var testLogger = new XPlatTestLogger();

            var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

            foreach (var filename in directoryFiles)
            {
                var filePath = Path.Combine(pathContext.SolutionRoot, filename);
                File.Create(filePath);
            }

            var whyCommandArgs = new WhyCommandArgs(
                path: pathContext.SolutionRoot,
                package: "package",
                frameworks: new List<string>(),
                logger: testLogger);

            var expectedErrorMessage = string.Format(
                                            CultureInfo.CurrentCulture,
                                            Strings.WhyCommand_Error_MultipleProjectOrSolutionFilesInDirectory,
                                            pathContext.SolutionRoot);

            // Act
            var projectList = whyCommandArgs.GetListOfProjectPaths();

            // Assert
            var errors = testLogger.ShowErrors();

            Assert.Null(projectList);
            Assert.Contains(expectedErrorMessage, errors);
        }

        [Fact]
        public void WhyCommand_GetListOfProjectPaths_WithDirectoryWithZeroSolutionsOrProjects_ReturnsNull()
        {
            // Arrange
            var pathContext = new SimpleTestPathContext();
            var testLogger = new XPlatTestLogger();

            var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

            var randomFilePath = Path.Combine(pathContext.SolutionRoot, "x.text");
            File.Create(randomFilePath);

            var whyCommandArgs = new WhyCommandArgs(
                path: pathContext.SolutionRoot,
                package: "package",
                frameworks: new List<string>(),
                logger: testLogger);

            var expectedErrorMessage = string.Format(
                                            CultureInfo.CurrentCulture,
                                            Strings.WhyCommand_Error_NoProjectOrSolutionFilesInDirectory,
                                            pathContext.SolutionRoot);

            // Act
            var projectList = whyCommandArgs.GetListOfProjectPaths();

            // Assert
            var errors = testLogger.ShowErrors();

            Assert.Null(projectList);
            Assert.Contains(expectedErrorMessage, errors);
        }

        private static void ConvertRelevantWindowsPathsToUnix(LockFile assetsFile)
        {
            assetsFile.PackageSpec.FilePath = ConvertWindowsPathToUnix(assetsFile.PackageSpec.FilePath);

            var projectLibraries = assetsFile.Libraries.Where(l => l.Type == "project");

            foreach (var library in projectLibraries)
            {
                library.Path = ConvertWindowsPathToUnix(library.Path);
            }

            var packageSpecTargets = assetsFile.PackageSpec.RestoreMetadata.TargetFrameworks;

            foreach (var target in packageSpecTargets)
            {
                var projectReferences = target.ProjectReferences;
                foreach (var projectReference in projectReferences)
                {
                    projectReference.ProjectPath = ConvertWindowsPathToUnix(projectReference.ProjectPath);
                }
            }
        }

        private static string ConvertWindowsPathToUnix(string path)
        {
            char[] trimChars = ['C', ':'];
            return path.TrimStart(trimChars).Replace("\\", "/");
        }
    }
}
