// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Moq;
using NuGet.CommandLine.XPlat;
using NuGet.Packaging;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    [Collection("NuGet XPlat Test Collection")]
    public class XPlatWhyTests
    {
        private static readonly string ProjectName = "Test.Project.DotnetNugetWhy";
        private static MSBuildAPIUtility MsBuild => new MSBuildAPIUtility(new TestCommandOutputLogger());

        [Fact]
        public async void WhyCommand_TransitiveDependency_PathExists()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var logger = new TestCommandOutputLogger();
                var projectFramework = "net472";
                var project = XPlatTestUtils.CreateProject(ProjectName, pathContext, projectFramework);

                var packageX = XPlatTestUtils.CreatePackage("PackageX", "1.0.0");
                var packageY = XPlatTestUtils.CreatePackage("PackageY", "1.0.1");

                packageX.Dependencies.Add(packageY);

                project.AddPackageToFramework(projectFramework, packageX);

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX,
                    packageY);

                var addPackageArgs = XPlatTestUtils.GetPackageReferenceArgs(packageX.Id, packageX.Version, project);
                var addPackageCommandRunner = new AddPackageReferenceCommandRunner();
                var addPackageResult = await addPackageCommandRunner.ExecuteCommand(addPackageArgs, MsBuild);

                var whyCommandArgs = new WhyCommandArgs(
                        project.ProjectPath,
                        packageY.Id,
                        [projectFramework],
                        logger);

                // Act
                var result = WhyCommandRunner.ExecuteCommand(whyCommandArgs);

                // Assert
                var output = logger.ShowMessages();

                Assert.Equal(ExitCodes.Success, result);
                Assert.Contains($"Project '{ProjectName}' has the following dependency graph(s) for '{packageY.Id}'", output);
            }
        }

        [Fact]
        public async void WhyCommand_TransitiveDependency_OutputFormatIsCorrect()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var logger = new Mock<ILoggerWithColor>();
                var projectFramework = "net472";
                var project = XPlatTestUtils.CreateProject(ProjectName, pathContext, projectFramework);

                var packageX = XPlatTestUtils.CreatePackage("PackageX", "1.0.0");
                var packageY = XPlatTestUtils.CreatePackage("PackageY", "1.0.1");

                packageX.Dependencies.Add(packageY);

                project.AddPackageToFramework(projectFramework, packageX);

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX,
                    packageY);

                var addPackageArgs = XPlatTestUtils.GetPackageReferenceArgs(packageX.Id, packageX.Version, project);
                var addPackageCommandRunner = new AddPackageReferenceCommandRunner();
                var addPackageResult = await addPackageCommandRunner.ExecuteCommand(addPackageArgs, MsBuild);

                var whyCommandArgs = new WhyCommandArgs(
                        project.ProjectPath,
                        packageY.Id,
                        [projectFramework],
                        logger.Object);

                // Act
                var result = WhyCommandRunner.ExecuteCommand(whyCommandArgs);

                // Assert
                Assert.Equal(ExitCodes.Success, result);

                logger.Verify(x => x.LogMinimal("Project 'Test.Project.DotnetNugetWhy' has the following dependency graph(s) for 'PackageY':"), Times.Exactly(1));
                logger.Verify(x => x.LogMinimal(""), Times.Exactly(2));
                logger.Verify(x => x.LogMinimal("	[net472]"), Times.Exactly(1));
                logger.Verify(x => x.LogMinimal("	 │  "), Times.Exactly(1));
                logger.Verify(x => x.LogMinimal("	 └─ PackageX (v1.0.0)"), Times.Exactly(1));
                logger.Verify(x => x.LogMinimal("	    └─ ", ConsoleColor.Gray), Times.Exactly(1));
                logger.Verify(x => x.LogMinimal("PackageY (v1.0.1)\n", ConsoleColor.Cyan), Times.Exactly(1));
            }
        }

        [Fact]
        public async void WhyCommand_BasicFunctionality_Succeeds()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var logger = new TestCommandOutputLogger();
                var projectFramework = "net472";
                var project = XPlatTestUtils.CreateProject(ProjectName, pathContext, projectFramework);

                var packageX = XPlatTestUtils.CreatePackage("PackageX", "1.0.0");
                var packageY = XPlatTestUtils.CreatePackage("PackageY", "1.0.1");

                packageX.Dependencies.Add(packageY);

                project.AddPackageToFramework(projectFramework, packageX);

                // Generate Package
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX,
                    packageY);

                var addPackageArgs = XPlatTestUtils.GetPackageReferenceArgs(packageX.Id, packageX.Version, project);
                var addPackageCommandRunner = new AddPackageReferenceCommandRunner();
                var addPackageResult = await addPackageCommandRunner.ExecuteCommand(addPackageArgs, MsBuild);

                var whyCommandArgs = new WhyCommandArgs(
                        project.ProjectPath,
                        packageY.Id,
                        [projectFramework],
                        logger);

                // Act
                var result = WhyCommandRunner.ExecuteCommand(whyCommandArgs);

                // Assert
                var output = logger.ShowMessages();

                Assert.Equal(ExitCodes.Success, result);
                Assert.Contains($"Project '{ProjectName}' has the following dependency graph(s) for '{packageY.Id}'", output);
            }
        }

        [Fact]
        public void WhyCommand_EmptyProjectArgument_Fails()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var logger = new TestCommandOutputLogger();
                var whyCommandArgs = new WhyCommandArgs(
                        "",
                        "PackageX",
                        [],
                        logger);

                // Act
                var result = WhyCommandRunner.ExecuteCommand(whyCommandArgs);

                // Assert
                var errorOutput = logger.ShowErrors();

                Assert.Equal(ExitCodes.InvalidArguments, result);
                Assert.Contains($"Unable to run 'dotnet nuget why'. The 'PROJECT|SOLUTION' argument cannot be empty.", errorOutput);
            }
        }

        [Fact]
        public void WhyCommand_EmptyPackageArgument_Fails()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var logger = new TestCommandOutputLogger();
                var projectFramework = "net472";
                var project = XPlatTestUtils.CreateProject(ProjectName, pathContext, projectFramework);

                var whyCommandArgs = new WhyCommandArgs(
                        project.ProjectPath,
                        "",
                        [],
                        logger);

                // Act
                var result = WhyCommandRunner.ExecuteCommand(whyCommandArgs);

                // Assert
                var errorOutput = logger.ShowErrors();

                Assert.Equal(ExitCodes.InvalidArguments, result);
                Assert.Contains($"Unable to run 'dotnet nuget why'. The 'PACKAGE' argument cannot be empty.", errorOutput);
            }
        }

        [Fact]
        public void WhyCommand_InvalidProject_Fails()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var logger = new TestCommandOutputLogger();
                var whyCommandArgs = new WhyCommandArgs(
                        "FakeProjectPath.csproj",
                        "PackageX",
                        [],
                        logger);

                // Act
                var result = WhyCommandRunner.ExecuteCommand(whyCommandArgs);

                // Assert
                var errorOutput = logger.ShowErrors();

                Assert.Equal(ExitCodes.InvalidArguments, result);
                Assert.Contains($"Unable to run 'dotnet nuget why'. Missing or invalid project/solution file 'FakeProjectPath.csproj'.", errorOutput);
            }
        }

        /*
        [Fact]
        public void NewTest()
        {
            var assetsFilePath = Path.Combine(pathContext.SolutionRoot, "obj", LockFileFormat.AssetsFileName);
            var format = new LockFileFormat();
            LockFile assetsFile = format.Read(assetsFilePath);

            var responses = new Dictionary<string, string>
            {
                { testFeedUrl, ProtocolUtility.GetResource("NuGet.PackageManagement.VisualStudio.Test.compiler.resources.index.json", GetType()) },
                { query + "?q=nuget&skip=0&take=26&prerelease=true&semVerLevel=2.0.0", ProtocolUtility.GetResource("NuGet.PackageManagement.VisualStudio.Test.compiler.resources.nugetSearchPage1.json", GetType()) },
                { query + "?q=nuget&skip=25&take=26&prerelease=true&semVerLevel=2.0.0", ProtocolUtility.GetResource("NuGet.PackageManagement.VisualStudio.Test.compiler.resources.nugetSearchPage2.json", GetType()) },
                { query + "?q=&skip=0&take=26&prerelease=true&semVerLevel=2.0.0", ProtocolUtility.GetResource("NuGet.PackageManagement.VisualStudio.Test.compiler.resources.blankSearchPage.json", GetType()) },
                { "https://api.nuget.org/v3/registration3-gz-semver2/nuget.core/index.json", ProtocolUtility.GetResource("NuGet.PackageManagement.VisualStudio.Test.compiler.resources.nugetCoreIndex.json", GetType()) },
                { "https://api.nuget.org/v3/registration3-gz-semver2/microsoft.extensions.logging.abstractions/index.json", ProtocolUtility.GetResource("NuGet.PackageManagement.VisualStudio.Test.compiler.resources.loggingAbstractions.json", GetType()) }
            };
        }
        */
    }
}
