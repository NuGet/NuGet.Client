// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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

                Assert.Equal(System.Threading.Tasks.Task.CompletedTask, result);
                Assert.Equal(string.Empty, logger.ShowErrors());

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
                var output = logger.ShowMessages();

                Assert.Equal(System.Threading.Tasks.Task.CompletedTask, result);
                Assert.Equal(string.Empty, logger.ShowErrors());

                Assert.Contains($"Project '{ProjectName}' has the following dependency graph(s) for 'PackageX'", output);
                Assert.DoesNotContain($"Project '{ProjectName}' does not have any dependency graph(s) for 'PackageX'", output);
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
                var output = logger.ShowMessages();

                Assert.Equal(System.Threading.Tasks.Task.CompletedTask, result);
                Assert.Equal(string.Empty, logger.ShowErrors());

                Assert.Contains($"Project '{ProjectName}' has the following dependency graph(s) for 'PackageX'", output);
                Assert.DoesNotContain($"Project '{ProjectName}' does not have any dependency graph(s) for 'PackageX'", output);
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
