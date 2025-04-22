// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using NuGet.CommandLine.XPlat;
using NuGet.CommandLine.XPlat.Commands.Why;
using NuGet.Packaging;
using NuGet.Test.Utility;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.XPlat.FuncTest
{
    [Collection("NuGet XPlat Test Collection")]
    public class XPlatWhyTests
    {
        private static readonly string ProjectName = "Test.Project.DotnetNugetWhy";

        private readonly ITestOutputHelper _testOutputHelper;

        public XPlatWhyTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task WhyCommand_ProjectHasTransitiveDependency_DependencyPathExists()
        {
            // Arrange
            var pathContext = new SimpleTestPathContext();
            var projectFramework = "net472";
            var project = XPlatTestUtils.CreateProject(ProjectName, pathContext, projectFramework);

            var packageX = XPlatTestUtils.CreatePackage("PackageX", "1.0.0");
            var packageY = XPlatTestUtils.CreatePackage("PackageY", "1.0.1");

            packageX.Dependencies.Add(packageY);

            project.AddPackageToFramework(projectFramework, packageX);

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                packageX,
                packageY);

            var logger = new TestCommandOutputLogger(_testOutputHelper);
            var reportRenderer = new WhyConsoleRenderer(logger);
            var addPackageArgs = XPlatTestUtils.GetPackageReferenceArgs(logger, packageX.Id, packageX.Version, project);
            var addPackageCommandRunner = new AddPackageReferenceCommandRunner();
            var addPackageResult = await addPackageCommandRunner.ExecuteCommand(addPackageArgs, new MSBuildAPIUtility(logger));

            var whyCommandArgs = new WhyCommandArgs(
                    project.ProjectPath,
                    packageY.Id,
                    [projectFramework],
                    reportRenderer,
                    logger,
                    CancellationToken.None);

            // Act
            var result = await WhyCommandRunner.ExecuteCommand(whyCommandArgs);

            // Assert
            var output = logger.ShowMessages();

            Assert.Equal(ExitCodes.Success, result);
            Assert.Contains($"Project '{ProjectName}' has the following dependency graph(s) for '{packageY.Id}'", output);
            Assert.Contains($"{packageX.Id} (v{packageX.Version})", output);
            Assert.Contains($"{packageY.Id} (v{packageY.Version})", output);
        }

        [Fact]
        public async Task WhyCommand_ProjectHasNoDependencyOnTargetPackage_PathDoesNotExist()
        {
            // Arrange
            var pathContext = new SimpleTestPathContext();
            var projectFramework = "net472";
            var project = XPlatTestUtils.CreateProject(ProjectName, pathContext, projectFramework);

            var packageX = XPlatTestUtils.CreatePackage("PackageX", "1.0.0");
            project.AddPackageToFramework(projectFramework, packageX);

            var packageZ = XPlatTestUtils.CreatePackage("PackageZ", "1.0.0"); // not added to project

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                packageX,
                packageZ);

            var logger = new TestCommandOutputLogger(_testOutputHelper);
            var reportRenderer = new WhyConsoleRenderer(logger);
            var addPackageArgs = XPlatTestUtils.GetPackageReferenceArgs(logger, packageX.Id, packageX.Version, project);
            var addPackageCommandRunner = new AddPackageReferenceCommandRunner();
            var addPackageResult = await addPackageCommandRunner.ExecuteCommand(addPackageArgs, new MSBuildAPIUtility(logger));

            var whyCommandArgs = new WhyCommandArgs(
                    project.ProjectPath,
                    packageZ.Id,
                    [projectFramework],
                    reportRenderer,
                    logger,
                    CancellationToken.None);

            // Act
            var result = await WhyCommandRunner.ExecuteCommand(whyCommandArgs);

            // Assert
            var output = logger.ShowMessages();

            Assert.Equal(ExitCodes.Success, result);
            Assert.Contains($"Project '{ProjectName}' does not have a dependency on '{packageZ.Id}'", output);
        }

        [Fact]
        public async Task WhyCommand_ProjectDidNotRunRestore_Fails()
        {
            // Arrange
            var logger = new TestCommandOutputLogger(_testOutputHelper);
            var reportRenderer = new WhyConsoleRenderer(logger);

            var pathContext = new SimpleTestPathContext();
            var projectFramework = "net472";
            var project = XPlatTestUtils.CreateProject(ProjectName, pathContext, projectFramework);

            var packageX = XPlatTestUtils.CreatePackage("PackageX", "1.0.0");
            var packageY = XPlatTestUtils.CreatePackage("PackageY", "1.0.1");

            packageX.Dependencies.Add(packageY);

            project.AddPackageToFramework(projectFramework, packageX);

            var whyCommandArgs = new WhyCommandArgs(
                    project.ProjectPath,
                    packageY.Id,
                    [projectFramework],
                    reportRenderer,
                    logger,
                    CancellationToken.None);

            // Act
            var result = await WhyCommandRunner.ExecuteCommand(whyCommandArgs);

            // Assert
            var output = logger.ShowMessages();

            Assert.Equal(ExitCodes.Success, result);
            Assert.Contains($"No assets file was found for `{project.ProjectPath}`. Please run restore before running this command.", output);
        }

        [Fact]
        public async Task WhyCommand_EmptyProjectArgument_Fails()
        {
            // Arrange
            var logger = new TestCommandOutputLogger(_testOutputHelper);
            var reportRenderer = new WhyConsoleRenderer(logger);

            var whyCommandArgs = new WhyCommandArgs(
                    "",
                    "PackageX",
                    [],
                    reportRenderer,
                    logger,
                    CancellationToken.None);

            // Act
            var result = await WhyCommandRunner.ExecuteCommand(whyCommandArgs);

            // Assert
            var errorOutput = logger.ShowErrors();

            Assert.Equal(ExitCodes.InvalidArguments, result);
            Assert.Contains($"Unable to run 'dotnet nuget why'. The 'PROJECT|SOLUTION' argument cannot be empty.", errorOutput);
        }

        [Fact]
        public async Task WhyCommand_EmptyPackageArgument_Fails()
        {
            // Arrange
            var logger = new TestCommandOutputLogger(_testOutputHelper);
            var reportRenderer = new WhyConsoleRenderer(logger);

            var pathContext = new SimpleTestPathContext();
            var projectFramework = "net472";
            var project = XPlatTestUtils.CreateProject(ProjectName, pathContext, projectFramework);

            var whyCommandArgs = new WhyCommandArgs(
                    project.ProjectPath,
                    "",
                    [],
                    reportRenderer,
                    logger,
                    CancellationToken.None);

            // Act
            var result = await WhyCommandRunner.ExecuteCommand(whyCommandArgs);

            // Assert
            var errorOutput = logger.ShowErrors();

            Assert.Equal(ExitCodes.InvalidArguments, result);
            Assert.Contains($"Unable to run 'dotnet nuget why'. The 'PACKAGE' argument cannot be empty.", errorOutput);
        }

        [Fact]
        public async Task WhyCommand_InvalidProject_Fails()
        {
            // Arrange
            var logger = new TestCommandOutputLogger(_testOutputHelper);
            var reportRenderer = new WhyConsoleRenderer(logger);

            string fakeProjectPath = "FakeProjectPath.csproj";

            var whyCommandArgs = new WhyCommandArgs(
                    fakeProjectPath,
                    "PackageX",
                    [],
                    reportRenderer,
                    logger,
                    CancellationToken.None);

            // Act
            var result = await WhyCommandRunner.ExecuteCommand(whyCommandArgs);

            // Assert
            var errorOutput = logger.ShowErrors();

            Assert.Equal(ExitCodes.InvalidArguments, result);
            Assert.Contains($"Unable to run 'dotnet nuget why'. Missing or invalid path '{fakeProjectPath}'. Please provide a path to a project, solution file, or directory.", errorOutput);
        }

        [Fact]
        public async Task WhyCommand_InvalidFrameworksOption_WarnsCorrectly()
        {
            // Arrange
            var pathContext = new SimpleTestPathContext();
            var projectFramework = "net472";
            var inputFrameworksOption = "invalidFrameworkAlias";
            var project = XPlatTestUtils.CreateProject(ProjectName, pathContext, projectFramework);

            var packageX = XPlatTestUtils.CreatePackage("PackageX", "1.0.0", projectFramework);
            var packageY = XPlatTestUtils.CreatePackage("PackageY", "1.0.1", projectFramework);

            packageX.Dependencies.Add(packageY);

            project.AddPackageToFramework(projectFramework, packageX);

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                packageX,
                packageY);

            var logger = new TestCommandOutputLogger(_testOutputHelper);
            var reportRenderer = new WhyConsoleRenderer(logger);
            var addPackageCommandArgs = XPlatTestUtils.GetPackageReferenceArgs(logger, packageX.Id, packageX.Version, project);
            var addPackageCommandRunner = new AddPackageReferenceCommandRunner();
            var addPackageResult = await addPackageCommandRunner.ExecuteCommand(addPackageCommandArgs, new MSBuildAPIUtility(logger));

            var whyCommandArgs = new WhyCommandArgs(
                    project.ProjectPath,
                    packageY.Id,
                    [inputFrameworksOption, projectFramework],
                    reportRenderer,
                    logger,
                    CancellationToken.None);

            // Act
            var result = await WhyCommandRunner.ExecuteCommand(whyCommandArgs);

            // Assert
            var output = logger.ShowMessages();

            Assert.Equal(ExitCodes.Success, result);
            Assert.Contains($"The assets file '{project.AssetsFileOutputPath}' for project '{ProjectName}' does not contain a target for the specified input framework '{inputFrameworksOption}'.", output);
            Assert.Contains($"Project '{ProjectName}' has the following dependency graph(s) for '{packageY.Id}'", output);
        }

        [Fact]
        public async Task WhyCommand_JsonRendererFrameworkNotFound_SucceedsAsync()
        {
            // Arrange
            var pathContext = new SimpleTestPathContext();
            var projectFramework = "net472";
            var projectFrameworkQuery = "net6.0";
            var project = XPlatTestUtils.CreateProject(ProjectName, pathContext, projectFramework);

            var packageX = XPlatTestUtils.CreatePackage("PackageX", "1.0.0");
            var packageY = XPlatTestUtils.CreatePackage("PackageY", "1.0.1");

            packageX.Dependencies.Add(packageY);

            project.AddPackageToFramework(projectFramework, packageX);

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                packageX,
                packageY);

            var logger = new TestCommandOutputLogger(_testOutputHelper);
            var reportRenderer = new WhyJsonRenderer(logger);
            var addPackageArgs = XPlatTestUtils.GetPackageReferenceArgs(logger, packageX.Id, packageX.Version, project);
            var addPackageCommandRunner = new AddPackageReferenceCommandRunner();
            var addPackageResult = await addPackageCommandRunner.ExecuteCommand(addPackageArgs, new MSBuildAPIUtility(logger));

            var whyCommandArgs = new WhyCommandArgs(
                    project.ProjectPath,
                    packageY.Id,
                    [projectFrameworkQuery],
                    reportRenderer,
                    logger,
                    CancellationToken.None);

            // Act
            var result = await WhyCommandRunner.ExecuteCommand(whyCommandArgs);

            // Assert
            var output = logger.ShowMessages();

            var expectedOutput = $@"{{
  ""version"": 1,
  ""parameters"": ""--framework {projectFrameworkQuery}"",
  ""project"": ""{project.ProjectName}"",
  ""package"": ""{packageY.Id}"",
  ""dependencyGraphs"": []
}}";

            Assert.Equal(ExitCodes.Success, result);
            Assert.Contains(expectedOutput, output);
        }

        [Fact]
        public async Task WhyCommand_JsonRendererDirectPackageFound_SucceedsAsync()
        {
            // Arrange
            var pathContext = new SimpleTestPathContext();
            var projectFramework = "net472";
            var project = XPlatTestUtils.CreateProject(ProjectName, pathContext, projectFramework);

            var packageX = XPlatTestUtils.CreatePackage("PackageX", "1.0.0");
            var packageY = XPlatTestUtils.CreatePackage("PackageY", "1.0.1");

            packageX.Dependencies.Add(packageY);

            project.AddPackageToFramework(projectFramework, packageX);

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                packageX,
                packageY);

            var logger = new TestCommandOutputLogger(_testOutputHelper);
            var reportRenderer = new WhyJsonRenderer(logger);
            var addPackageArgs = XPlatTestUtils.GetPackageReferenceArgs(logger, packageX.Id, packageX.Version, project);
            var addPackageCommandRunner = new AddPackageReferenceCommandRunner();
            var addPackageResult = await addPackageCommandRunner.ExecuteCommand(addPackageArgs, new MSBuildAPIUtility(logger));

            var whyCommandArgs = new WhyCommandArgs(
                    project.ProjectPath,
                    packageX.Id,
                    [projectFramework],
                    reportRenderer,
                    logger,
                    CancellationToken.None);

            // Act
            var result = await WhyCommandRunner.ExecuteCommand(whyCommandArgs);

            // Assert
            var output = logger.ShowMessages();

            var expectedOutput = $@"{{
  ""version"": 1,
  ""parameters"": ""--framework {projectFramework}"",
  ""project"": ""{project.ProjectName}"",
  ""package"": ""{packageX.Id}"",
  ""dependencyGraphs"": [
    {{
      ""framework"": ""{projectFramework}"",
      ""dependencies"": [
        {{
          ""id"": ""{packageX.Id}"",
          ""version"": ""{packageX.Version}"",
          ""dependencies"": []
        }}
      ]
    }}
  ]
}}";

            Assert.Equal(ExitCodes.Success, result);
            Assert.Contains(expectedOutput, output);
        }

        [Fact]
        public async Task WhyCommand_JsonRendererTransitivePackageFound_SucceedsAsync()
        {
            // Arrange
            var pathContext = new SimpleTestPathContext();
            var projectFramework = "net472";
            var project = XPlatTestUtils.CreateProject(ProjectName, pathContext, projectFramework);

            var packageX = XPlatTestUtils.CreatePackage("PackageX", "1.0.0");
            var packageY = XPlatTestUtils.CreatePackage("PackageY", "1.0.1");

            packageX.Dependencies.Add(packageY);

            project.AddPackageToFramework(projectFramework, packageX);

            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                pathContext.PackageSource,
                PackageSaveMode.Defaultv3,
                packageX,
                packageY);

            var logger = new TestCommandOutputLogger(_testOutputHelper);
            var reportRenderer = new WhyJsonRenderer(logger);
            var addPackageArgs = XPlatTestUtils.GetPackageReferenceArgs(logger, packageX.Id, packageX.Version, project);
            var addPackageCommandRunner = new AddPackageReferenceCommandRunner();
            var addPackageResult = await addPackageCommandRunner.ExecuteCommand(addPackageArgs, new MSBuildAPIUtility(logger));

            var whyCommandArgs = new WhyCommandArgs(
                    project.ProjectPath,
                    packageY.Id,
                    [projectFramework],
                    reportRenderer,
                    logger,
                    CancellationToken.None);

            // Act
            var result = await WhyCommandRunner.ExecuteCommand(whyCommandArgs);

            // Assert
            var output = logger.ShowMessages();

            var expectedOutput = $@"{{
  ""version"": 1,
  ""parameters"": ""--framework {projectFramework}"",
  ""project"": ""{project.ProjectName}"",
  ""package"": ""{packageY.Id}"",
  ""dependencyGraphs"": [
    {{
      ""framework"": ""{projectFramework}"",
      ""dependencies"": [
        {{
          ""id"": ""{packageX.Id}"",
          ""version"": ""{packageX.Version}"",
          ""dependencies"": [
            {{
              ""id"": ""{packageY.Id}"",
              ""version"": ""{packageY.Version}"",
              ""dependencies"": []
            }}
          ]
        }}
      ]
    }}
  ]
}}";

            Assert.Equal(ExitCodes.Success, result);
            Assert.Contains(expectedOutput, output);
        }
    }
}
