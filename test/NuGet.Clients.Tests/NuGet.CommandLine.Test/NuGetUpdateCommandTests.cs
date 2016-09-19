// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetUpdateCommandTests
    {
        [Fact]
        public async Task UpdateCommand_Success_Update_DeletedFile()
        {
            using (var packagesSourceDirectory = TestDirectory.Create())
            using (var solutionDirectory = TestDirectory.Create())
            using (var workingPath = TestDirectory.Create())
            {
                var projectDirectory = Path.Combine(solutionDirectory, "proj1");
                var packagesDirectory = Path.Combine(solutionDirectory, "packages");
                
                var a1 = new PackageIdentity("A", new NuGetVersion("1.0.0"));
                var a2 = new PackageIdentity("A", new NuGetVersion("2.0.0"));
                var b1 = new PackageIdentity("B", new NuGetVersion("1.0.0"));
                var b2 = new PackageIdentity("B", new NuGetVersion("2.0.0"));
                
                var a1Package = Util.CreateTestPackage(
                    a1.Id,
                    a1.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework> { NuGetFramework.Parse("net45") },
                    "test.txt"
                    );

                var a2Package = Util.CreateTestPackage(
                    a2.Id,
                    a2.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework> { NuGetFramework.Parse("net45") },
                   "test.txt"
                    );

                var b1Package = Util.CreateTestPackage(
                    b1.Id,
                    b1.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework> { NuGetFramework.Parse("net45") },
                    "test.txt"
                    );

                var b2Package = Util.CreateTestPackage(
                    b2.Id,
                    b2.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework> { NuGetFramework.Parse("net45") },
                    "test.txt"
                    );
                
                Directory.CreateDirectory(projectDirectory);
                
                Util.CreateFile(
                    projectDirectory,
                    "proj1.csproj",
                    Util.CreateProjFileContent(contentFiles: new[] { "test.txt" }));

                Util.CreateFile(solutionDirectory, "a.sln",
                    Util.CreateSolutionFileContent());

                var projectFile = Path.Combine(projectDirectory, "proj1.csproj");
                var solutionFile = Path.Combine(solutionDirectory, "a.sln");

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msbuildDirectory = MsBuildUtility.GetMsBuildDirectory("14.0", null);
                var projectSystem = new MSBuildProjectSystem(msbuildDirectory, projectFile, testNuGetProjectContext);
                var msBuildProject = new MSBuildNuGetProject(projectSystem, packagesDirectory, projectDirectory);
                using (var stream = File.OpenRead(a1Package))
                {
                    var downloadResult = new DownloadResourceResult(stream);
                    await msBuildProject.InstallPackageAsync(
                        a1,
                        downloadResult,
                        testNuGetProjectContext,
                        CancellationToken.None);
                }

                using (var stream = File.OpenRead(b1Package))
                {
                    var downloadResult = new DownloadResourceResult(stream);
                    await msBuildProject.InstallPackageAsync(
                        b1,
                        downloadResult,
                        testNuGetProjectContext,
                        CancellationToken.None);
                }
                
                var args = new[]
                {
                    "update",
                    solutionFile,
                    "-Source",
                    packagesSourceDirectory,
                    "-NonInteractive"
                };

                var r = CommandRunner.Run(
                    Util.GetNuGetExePath(),
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                Assert.True(r.Item1 == 0, "Output is " + r.Item2 + ". Error is " + r.Item3);

                var content = File.ReadAllText(projectFile);
                Assert.True(content.Contains(@"<HintPath>..\packages\B.2.0.0\lib\net45\B.dll</HintPath>"));
                Assert.True(content.Contains(@"<HintPath>..\packages\A.2.0.0\lib\net45\A.dll</HintPath>"));
                Assert.True(content.Contains(@"<Content Include=""test.txt"" />"));
            }
        }

        [Fact]
        public async Task UpdateCommand_Success_References()
        {
            using (var packagesSourceDirectory = TestDirectory.Create())
            using (var solutionDirectory = TestDirectory.Create())
            using (var workingPath = TestDirectory.Create())
            {
                var projectDirectory = Path.Combine(solutionDirectory, "proj1");
                var packagesDirectory = Path.Combine(solutionDirectory, "packages");

                var a1 = new PackageIdentity("A", new NuGetVersion("1.0.0"));
                var a2 = new PackageIdentity("A", new NuGetVersion("2.0.0"));

                var a1Package = Util.CreateTestPackage(
                    a1.Id,
                    a1.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });


                var a2Package = Util.CreateTestPackage(
                    a2.Id,
                    a2.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });

                var packagesFolder = PathUtility.GetRelativePath(projectDirectory, packagesDirectory);

                Directory.CreateDirectory(projectDirectory);
                // create project 1
                Util.CreateFile(
                    projectDirectory,
                    "proj1.csproj",
                    Util.CreateProjFileContent());

                Util.CreateFile(solutionDirectory, "a.sln",
                    Util.CreateSolutionFileContent());

                var projectFile = Path.Combine(projectDirectory, "proj1.csproj");
                var solutionFile = Path.Combine(solutionDirectory, "a.sln");

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msbuildDirectory = MsBuildUtility.GetMsBuildDirectory("14.0", null);
                var projectSystem = new MSBuildProjectSystem(msbuildDirectory, projectFile, testNuGetProjectContext);
                var msBuildProject = new MSBuildNuGetProject(projectSystem, packagesDirectory, projectDirectory);
                using (var stream = File.OpenRead(a1Package))
                {
                    var downloadResult = new DownloadResourceResult(stream);
                    await msBuildProject.InstallPackageAsync(
                        a1,
                        downloadResult,
                        testNuGetProjectContext,
                        CancellationToken.None);
                }

                var args = new[]
                {
                    "update",
                    solutionFile,
                    "-Source",
                    packagesSourceDirectory
                };

                var r = CommandRunner.Run(
                    Util.GetNuGetExePath(),
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                Assert.True(r.Item1 == 0, "Output is " + r.Item2 + ". Error is " + r.Item3);

                var content = File.ReadAllText(projectFile);
                Assert.False(content.Contains(@"<HintPath>..\packages\A.1.0.0\lib\net45\file.dll</HintPath>"));
                Assert.True(content.Contains(@"<HintPath>..\packages\A.2.0.0\lib\net45\file.dll</HintPath>"));
            }
        }

        [Fact]
        public async Task UpdateCommand_Success_References_MultipleProjects()
        {

            using (var packagesSourceDirectory = TestDirectory.Create())
            using (var solutionDirectory = TestDirectory.Create())
            using (var workingPath = TestDirectory.Create())
            {
                var projectDirectory1 = Path.Combine(solutionDirectory, "proj1");
                var projectDirectory2 = Path.Combine(solutionDirectory, "proj2");
                var packagesDirectory = Path.Combine(solutionDirectory, "packages");

                var a1 = new PackageIdentity("A", new NuGetVersion("1.0.0"));
                var a2 = new PackageIdentity("A", new NuGetVersion("2.0.0"));

                var b1 = new PackageIdentity("B", new NuGetVersion("1.0.0"));
                var b2 = new PackageIdentity("B", new NuGetVersion("2.0.0"));

                var a1Package = Util.CreateTestPackage(
                    a1.Id,
                    a1.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });

                var a2Package = Util.CreateTestPackage(
                    a2.Id,
                    a2.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });

                var b1Package = Util.CreateTestPackage(
                    b1.Id,
                    b1.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });

                var b2Package = Util.CreateTestPackage(
                    b2.Id,
                    b2.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });

                Directory.CreateDirectory(projectDirectory1);

                Util.CreateFile(
                    projectDirectory1,
                    "proj1.csproj",
                    Util.CreateProjFileContent());

                Directory.CreateDirectory(projectDirectory2);

                Util.CreateFile(
                    projectDirectory2,
                    "proj2.csproj",
                    Util.CreateProjFileContent());

                Util.CreateFile(solutionDirectory, "a.sln",
                    Util.CreateSolutionFileContent());

                var projectFile1 = Path.Combine(projectDirectory1, "proj1.csproj");
                var projectFile2 = Path.Combine(projectDirectory2, "proj2.csproj");
                var solutionFile = Path.Combine(solutionDirectory, "a.sln");

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msbuildDirectory = MsBuildUtility.GetMsBuildDirectory("14.0", null);
                var projectSystem1 = new MSBuildProjectSystem(msbuildDirectory, projectFile1, testNuGetProjectContext);
                var projectSystem2 = new MSBuildProjectSystem(msbuildDirectory, projectFile2, testNuGetProjectContext);
                var msBuildProject1 = new MSBuildNuGetProject(projectSystem1, packagesDirectory, projectDirectory1);
                var msBuildProject2 = new MSBuildNuGetProject(projectSystem2, packagesDirectory, projectDirectory2);

                using (var stream = File.OpenRead(a1Package))
                {
                    var downloadResult = new DownloadResourceResult(stream);
                    await msBuildProject1.InstallPackageAsync(
                        a1,
                        downloadResult,
                        testNuGetProjectContext,
                        CancellationToken.None);
                }

                using (var stream = File.OpenRead(b1Package))
                {
                    var downloadResult = new DownloadResourceResult(stream);
                    await msBuildProject2.InstallPackageAsync(
                        b1,
                        downloadResult,
                        testNuGetProjectContext,
                        CancellationToken.None);
                }

                projectSystem1.Save();
                projectSystem2.Save();

                var args = new[]
                {
                    "update",
                    solutionFile,
                    "-Source",
                    packagesSourceDirectory,
                    "-Id",
                    "A"
                };

                var r = CommandRunner.Run(
                    Util.GetNuGetExePath(),
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                Assert.True(r.Item1 == 0, "Output is " + r.Item2 + ". Error is " + r.Item3);

                var content1 = File.ReadAllText(projectFile1);
                Assert.False(content1.Contains(@"<HintPath>..\packages\A.1.0.0\lib\net45\file.dll</HintPath>"));
                Assert.True(content1.Contains(@"<HintPath>..\packages\A.2.0.0\lib\net45\file.dll</HintPath>"));
                Assert.False(content1.Contains(@"<HintPath>..\packages\B.1.0.0\lib\net45\file.dll</HintPath>"));
                Assert.False(content1.Contains(@"<HintPath>..\packages\B.2.0.0\lib\net45\file.dll</HintPath>"));

                var content2 = File.ReadAllText(projectFile2);
                Assert.True(content2.Contains(@"<HintPath>..\packages\B.1.0.0\lib\net45\file.dll</HintPath>"));
                Assert.False(content2.Contains(@"<HintPath>..\packages\B.2.0.0\lib\net45\file.dll</HintPath>"));
                Assert.False(content2.Contains(@"<HintPath>..\packages\A.1.0.0\lib\net45\file.dll</HintPath>"));
                Assert.False(content2.Contains(@"<HintPath>..\packages\A.2.0.0\lib\net45\file.dll</HintPath>"));
            }
        }

        [Fact]
        public async Task UpdateCommand_Fails_References_MultipleProjectsInSameDirectory()
        {
            // Arrange
            using (var packagesSourceDirectory = TestDirectory.Create())
            using (var solutionDirectory = TestDirectory.Create())
            using (var workingPath = TestDirectory.Create())
            {
                var projectDirectory = Path.Combine(solutionDirectory, "proj");
                var packagesDirectory = Path.Combine(solutionDirectory, "packages");

                var a1 = new PackageIdentity("A", new NuGetVersion("1.0.0"));
                var a2 = new PackageIdentity("A", new NuGetVersion("2.0.0"));

                var a1Package = Util.CreateTestPackage(
                    a1.Id,
                    a1.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });

                var a2Package = Util.CreateTestPackage(
                    a2.Id,
                    a2.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });

                Directory.CreateDirectory(projectDirectory);

                Util.CreateFile(
                    projectDirectory,
                    "proj1.csproj",
                    Util.CreateProjFileContent());

                Util.CreateFile(solutionDirectory, "a.sln",
                    Util.CreateSolutionFileContent());

                var projectFile1 = Path.Combine(projectDirectory, "proj1.csproj");
                var packagesConfigFile = Path.Combine(projectDirectory, "packages.config");
                var solutionFile = Path.Combine(solutionDirectory, "a.sln");

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msbuildDirectory = MsBuildUtility.GetMsBuildDirectory("14.0", null);
                var projectSystem1 = new MSBuildProjectSystem(msbuildDirectory, projectFile1, testNuGetProjectContext);
                var msBuildProject1 = new MSBuildNuGetProject(projectSystem1, packagesDirectory, projectDirectory);

                using (var stream = File.OpenRead(a1Package))
                {
                    var downloadResult = new DownloadResourceResult(stream);
                    await msBuildProject1.InstallPackageAsync(
                        a1,
                        downloadResult,
                        testNuGetProjectContext,
                        CancellationToken.None);
                }

                projectSystem1.Save();

                var projectFile2 = Path.Combine(projectDirectory, "proj2.csproj");
                File.Copy(projectFile1, projectFile2);

                var args = new[]
                {
                    "update",
                    solutionFile,
                    "-Source",
                    packagesSourceDirectory,
                    "-Id",
                    "A"
                };

                // Act
                var r = CommandRunner.Run(
                    Util.GetNuGetExePath(),
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.True(r.Item1 == 0, "Output is " + r.Item2 + ". Error is " + r.Item3);

                Assert.Contains("Scanning for projects...", r.Item2);
                Assert.Contains($"WARNING: Found multiple project files for '{packagesConfigFile}'.", r.Item2);
                Assert.Contains("No projects found with packages.config.", r.Item2);

                var content1 = File.ReadAllText(projectFile1);
                Assert.True(content1.Contains(@"<HintPath>..\packages\A.1.0.0\lib\net45\file.dll</HintPath>"));
                Assert.False(content1.Contains(@"<HintPath>..\packages\A.2.0.0\lib\net45\file.dll</HintPath>"));

                var content2 = File.ReadAllText(projectFile2);
                Assert.True(content2.Contains(@"<HintPath>..\packages\A.1.0.0\lib\net45\file.dll</HintPath>"));
                Assert.False(content2.Contains(@"<HintPath>..\packages\A.2.0.0\lib\net45\file.dll</HintPath>"));
            }
        }

        [Fact]
        public async Task UpdateCommand_Success_NOPrerelease()
        {
            using (var packagesSourceDirectory = TestDirectory.Create())
            using (var solutionDirectory = TestDirectory.Create())
            using (var workingPath = TestDirectory.Create())
            {
                var projectDirectory = Path.Combine(solutionDirectory, "proj1");
                var packagesDirectory = Path.Combine(solutionDirectory, "packages");

                var a1 = new PackageIdentity("A", new NuGetVersion("1.0.0"));
                var a2 = new PackageIdentity("A", new NuGetVersion("2.0.0-beta"));

                var a1Package = Util.CreateTestPackage(
                    a1.Id,
                    a1.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });


                var a2Package = Util.CreateTestPackage(
                    a2.Id,
                    a2.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });

                var packagesFolder = PathUtility.GetRelativePath(projectDirectory, packagesDirectory);

                Directory.CreateDirectory(projectDirectory);
                // create project 1
                Util.CreateFile(
                    projectDirectory,
                    "proj1.csproj",
                    Util.CreateProjFileContent());

                Util.CreateFile(solutionDirectory, "a.sln",
                    Util.CreateSolutionFileContent());

                var projectFile = Path.Combine(projectDirectory, "proj1.csproj");
                var solutionFile = Path.Combine(solutionDirectory, "a.sln");

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msbuildDirectory = MsBuildUtility.GetMsBuildDirectory("14.0", null);
                var projectSystem = new MSBuildProjectSystem(msbuildDirectory, projectFile, testNuGetProjectContext);
                var msBuildProject = new MSBuildNuGetProject(projectSystem, packagesDirectory, projectDirectory);
                using (var stream = File.OpenRead(a1Package))
                {
                    var downloadResult = new DownloadResourceResult(stream);
                    await msBuildProject.InstallPackageAsync(
                        a1,
                        downloadResult,
                        testNuGetProjectContext,
                        CancellationToken.None);
                }

                var args = new[]
                {
                    "update",
                    solutionFile,
                    "-Source",
                    packagesSourceDirectory,
                };

                var r = CommandRunner.Run(
                    Util.GetNuGetExePath(),
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                Assert.True(r.Item1 == 0, "Output is " + r.Item2 + ". Error is " + r.Item3);

                var content = File.ReadAllText(projectFile);
                Assert.False(content.Contains(@"<HintPath>..\packages\A.2.0.0-beta\lib\net45\file.dll</HintPath>"));
            }
        }

        [Fact]
        public async Task UpdateCommand_Success_Prerelease()
        {
            using (var packagesSourceDirectory = TestDirectory.Create())
            using (var solutionDirectory = TestDirectory.Create())
            using (var workingPath = TestDirectory.Create())
            {
                var projectDirectory = Path.Combine(solutionDirectory, "proj1");
                var packagesDirectory = Path.Combine(solutionDirectory, "packages");

                var a1 = new PackageIdentity("A", new NuGetVersion("1.0.0"));
                var a2 = new PackageIdentity("A", new NuGetVersion("2.0.0-BETA"));

                var a1Package = Util.CreateTestPackage(
                    a1.Id,
                    a1.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });


                var a2Package = Util.CreateTestPackage(
                    a2.Id,
                    a2.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });

                var packagesFolder = PathUtility.GetRelativePath(projectDirectory, packagesDirectory);

                Directory.CreateDirectory(projectDirectory);
                // create project 1
                Util.CreateFile(
                    projectDirectory,
                    "proj1.csproj",
                    Util.CreateProjFileContent());

                Util.CreateFile(solutionDirectory, "a.sln",
                    Util.CreateSolutionFileContent());

                var projectFile = Path.Combine(projectDirectory, "proj1.csproj");
                var solutionFile = Path.Combine(solutionDirectory, "a.sln");

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msbuildDirectory = MsBuildUtility.GetMsBuildDirectory("14.0", null);
                var projectSystem = new MSBuildProjectSystem(msbuildDirectory, projectFile, testNuGetProjectContext);
                var msBuildProject = new MSBuildNuGetProject(projectSystem, packagesDirectory, projectDirectory);
                using (var stream = File.OpenRead(a1Package))
                {
                    var downloadResult = new DownloadResourceResult(stream);
                    await msBuildProject.InstallPackageAsync(
                        a1,
                        downloadResult,
                        testNuGetProjectContext,
                        CancellationToken.None);
                }

                var args = new[]
                {
                    "update",
                    solutionFile,
                    "-Source",
                    packagesSourceDirectory,
                    "-Prerelease",
                };

                var r = CommandRunner.Run(
                    Util.GetNuGetExePath(),
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                Assert.True(r.Item1 == 0, "Output is " + r.Item2 + ". Error is " + r.Item3);

                var content = File.ReadAllText(projectFile);
                Assert.False(content.Contains(@"<HintPath>..\packages\A.1.0.0\lib\net45\file.dll</HintPath>"));
                Assert.True(content.Contains(@"<HintPath>..\packages\A.2.0.0-BETA\lib\net45\file.dll</HintPath>"));
            }
        }

        [Fact]
        public async Task UpdateCommand_Success_Version_Upgrade()
        {
            using (var packagesSourceDirectory = TestDirectory.Create())
            using (var solutionDirectory = TestDirectory.Create())
            using (var workingPath = TestDirectory.Create())
            {
                var projectDirectory = Path.Combine(solutionDirectory, "proj1");
                var packagesDirectory = Path.Combine(solutionDirectory, "packages");

                var a1 = new PackageIdentity("A", new NuGetVersion("1.0.0"));
                var a2 = new PackageIdentity("A", new NuGetVersion("2.0.0"));
                var a3 = new PackageIdentity("A", new NuGetVersion("3.0.0"));

                var a1Package = Util.CreateTestPackage(
                    a1.Id,
                    a1.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });
                
                var a2Package = Util.CreateTestPackage(
                    a2.Id,
                    a2.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });

                var a3Package = Util.CreateTestPackage(
                    a3.Id,
                    a3.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });

                var packagesFolder = PathUtility.GetRelativePath(projectDirectory, packagesDirectory);

                Directory.CreateDirectory(projectDirectory);
                // create project 1
                Util.CreateFile(
                    projectDirectory,
                    "proj1.csproj",
                    Util.CreateProjFileContent());

                Util.CreateFile(solutionDirectory, "a.sln",
                    Util.CreateSolutionFileContent());

                var projectFile = Path.Combine(projectDirectory, "proj1.csproj");
                var solutionFile = Path.Combine(solutionDirectory, "a.sln");

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msbuildDirectory = MsBuildUtility.GetMsBuildDirectory("14.0", null);
                var projectSystem = new MSBuildProjectSystem(msbuildDirectory, projectFile, testNuGetProjectContext);
                var msBuildProject = new MSBuildNuGetProject(projectSystem, packagesDirectory, projectDirectory);
                using (var stream = File.OpenRead(a1Package))
                {
                    var downloadResult = new DownloadResourceResult(stream);
                    await msBuildProject.InstallPackageAsync(
                        a1,
                        downloadResult,
                        testNuGetProjectContext,
                        CancellationToken.None);
                }

                var args = new[]
                {
                    "update",
                    solutionFile,
                    "-Source",
                    packagesSourceDirectory,
                    "-Version",
                    "2.0.0",
                    "-Id",
                    "A"
                };

                var r = CommandRunner.Run(
                    Util.GetNuGetExePath(),
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                Assert.True(r.Item1 == 0, "Output is " + r.Item2 + ". Error is " + r.Item3);

                var content = File.ReadAllText(projectFile);
                Assert.False(content.Contains(@"<HintPath>..\packages\A.1.0.0\lib\net45\file.dll</HintPath>"));
                Assert.True(content.Contains(@"<HintPath>..\packages\A.2.0.0\lib\net45\file.dll</HintPath>"));
                Assert.False(content.Contains(@"<HintPath>..\packages\A.3.0.0\lib\net45\file.dll</HintPath>"));
            }
        }

        [Fact]
        public async Task UpdateCommand_Success_Version_Downgrade()
        {
            using (var packagesSourceDirectory = TestDirectory.Create())
            using (var solutionDirectory = TestDirectory.Create())
            using (var workingPath = TestDirectory.Create())
            {
                var projectDirectory = Path.Combine(solutionDirectory, "proj1");
                var packagesDirectory = Path.Combine(solutionDirectory, "packages");

                var a1 = new PackageIdentity("A", new NuGetVersion("1.0.0"));
                var a2 = new PackageIdentity("A", new NuGetVersion("2.0.0"));
                var a3 = new PackageIdentity("A", new NuGetVersion("3.0.0"));

                var a1Package = Util.CreateTestPackage(
                    a1.Id,
                    a1.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });

                var a2Package = Util.CreateTestPackage(
                    a2.Id,
                    a2.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });

                var a3Package = Util.CreateTestPackage(
                    a3.Id,
                    a3.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });

                var packagesFolder = PathUtility.GetRelativePath(projectDirectory, packagesDirectory);

                Directory.CreateDirectory(projectDirectory);
                // create project 1
                Util.CreateFile(
                    projectDirectory,
                    "proj1.csproj",
                    Util.CreateProjFileContent());

                Util.CreateFile(solutionDirectory, "a.sln",
                    Util.CreateSolutionFileContent());

                var projectFile = Path.Combine(projectDirectory, "proj1.csproj");
                var solutionFile = Path.Combine(solutionDirectory, "a.sln");

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msbuildDirectory = MsBuildUtility.GetMsBuildDirectory("14.0", null);
                var projectSystem = new MSBuildProjectSystem(msbuildDirectory, projectFile, testNuGetProjectContext);
                var msBuildProject = new MSBuildNuGetProject(projectSystem, packagesDirectory, projectDirectory);
                using (var stream = File.OpenRead(a2Package))
                {
                    var downloadResult = new DownloadResourceResult(stream);
                    await msBuildProject.InstallPackageAsync(
                        a2,
                        downloadResult,
                        testNuGetProjectContext,
                        CancellationToken.None);
                }

                var args = new[]
                {
                    "update",
                    solutionFile,
                    "-Source",
                    packagesSourceDirectory,
                    "-Version",
                    "1.0.0",
                    "-Id",
                    "A"
                };

                var r = CommandRunner.Run(
                    Util.GetNuGetExePath(),
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                Assert.True(r.Item1 == 0, "Output is " + r.Item2 + ". Error is " + r.Item3);

                var content = File.ReadAllText(projectFile);
                Assert.True(content.Contains(@"<HintPath>..\packages\A.1.0.0\lib\net45\file.dll</HintPath>"));
                Assert.False(content.Contains(@"<HintPath>..\packages\A.2.0.0\lib\net45\file.dll</HintPath>"));
                Assert.False(content.Contains(@"<HintPath>..\packages\A.3.0.0\lib\net45\file.dll</HintPath>"));
            }
        }

        [Fact]
        public async Task UpdateCommand_Success_ProjectFile_References()
        {
            using (var packagesSourceDirectory = TestDirectory.Create())
            using (var solutionDirectory = TestDirectory.Create())
            using (var workingPath = TestDirectory.Create())
            {
                var projectDirectory = Path.Combine(solutionDirectory, "proj1");
                var packagesDirectory = Path.Combine(solutionDirectory, "packages");

                var a1 = new PackageIdentity("A", new NuGetVersion("1.0.0"));
                var a2 = new PackageIdentity("A", new NuGetVersion("2.0.0"));

                var a1Package = Util.CreateTestPackage(
                    a1.Id,
                    a1.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });


                var a2Package = Util.CreateTestPackage(
                    a2.Id,
                    a2.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });

                var packagesFolder = PathUtility.GetRelativePath(projectDirectory, packagesDirectory);

                Directory.CreateDirectory(projectDirectory);
                // create project 1
                Util.CreateFile(
                    projectDirectory,
                    "proj1.csproj",
                    Util.CreateProjFileContent());

                Util.CreateFile(solutionDirectory, "a.sln",
                    Util.CreateSolutionFileContent());

                var projectFile = Path.Combine(projectDirectory, "proj1.csproj");
                var solutionFile = Path.Combine(solutionDirectory, "a.sln");

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msbuildDirectory = MsBuildUtility.GetMsBuildDirectory("14.0", null);
                var projectSystem = new MSBuildProjectSystem(msbuildDirectory, projectFile, testNuGetProjectContext);
                var msBuildProject = new MSBuildNuGetProject(projectSystem, packagesDirectory, projectDirectory);
                using (var stream = File.OpenRead(a1Package))
                {
                    var downloadResult = new DownloadResourceResult(stream);
                    await msBuildProject.InstallPackageAsync(
                        a1,
                        downloadResult,
                        testNuGetProjectContext,
                        CancellationToken.None);
                }

                var args = new[]
                {
                    "update",
                    projectFile,
                    "-Source",
                    packagesSourceDirectory
                };

                var r = CommandRunner.Run(
                    Util.GetNuGetExePath(),
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                Assert.True(r.Item1 == 0, "Output is " + r.Item2 + ". Error is " + r.Item3);

                var content = File.ReadAllText(projectFile);
                Assert.False(content.Contains(@"<HintPath>..\packages\A.1.0.0\lib\net45\file.dll</HintPath>"));
                Assert.True(content.Contains(@"<HintPath>..\packages\A.2.0.0\lib\net45\file.dll</HintPath>"));
            }
        }

        [Fact]
        public async Task UpdateCommand_Success_PackagesConfig_References()
        {
            using (var packagesSourceDirectory = TestDirectory.Create())
            using (var solutionDirectory = TestDirectory.Create())
            using (var workingPath = TestDirectory.Create())
            {
                var projectDirectory = Path.Combine(solutionDirectory, "proj1");
                var packagesDirectory = Path.Combine(solutionDirectory, "packages");

                var a1 = new PackageIdentity("A", new NuGetVersion("1.0.0"));
                var a2 = new PackageIdentity("A", new NuGetVersion("2.0.0"));

                var a1Package = Util.CreateTestPackage(
                    a1.Id,
                    a1.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });


                var a2Package = Util.CreateTestPackage(
                    a2.Id,
                    a2.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });

                var packagesFolder = PathUtility.GetRelativePath(projectDirectory, packagesDirectory);

                Directory.CreateDirectory(projectDirectory);
                // create project 1
                Util.CreateFile(
                    projectDirectory,
                    "proj1.csproj",
                    Util.CreateProjFileContent());

                Util.CreateFile(solutionDirectory, "a.sln",
                    Util.CreateSolutionFileContent());

                var projectFile = Path.Combine(projectDirectory, "proj1.csproj");
                var solutionFile = Path.Combine(solutionDirectory, "a.sln");
                var packagesConfigFile = Path.Combine(projectDirectory, "packages.config");

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msbuildDirectory = MsBuildUtility.GetMsBuildDirectory("14.0", null);
                var projectSystem = new MSBuildProjectSystem(msbuildDirectory, projectFile, testNuGetProjectContext);
                var msBuildProject = new MSBuildNuGetProject(projectSystem, packagesDirectory, projectDirectory);
                using (var stream = File.OpenRead(a1Package))
                {
                    var downloadResult = new DownloadResourceResult(stream);
                    await msBuildProject.InstallPackageAsync(
                        a1,
                        downloadResult,
                        testNuGetProjectContext,
                        CancellationToken.None);
                }

                var args = new[]
                {
                    "update",
                    projectFile,
                    "-Source",
                    packagesSourceDirectory
                };

                var r = CommandRunner.Run(
                    Util.GetNuGetExePath(),
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                Assert.True(r.Item1 == 0, "Output is " + r.Item2 + ". Error is " + r.Item3);

                var content = File.ReadAllText(projectFile);
                Assert.False(content.Contains(@"<HintPath>..\packages\A.1.0.0\lib\net45\file.dll</HintPath>"));
                Assert.True(content.Contains(@"<HintPath>..\packages\A.2.0.0\lib\net45\file.dll</HintPath>"));
            }
        }

        [Fact]
        public async Task UpdateCommand_Success_ContentFiles()
        {
            // Arrange
            using (var packagesSourceDirectory = TestDirectory.Create())
            using (var solutionDirectory = TestDirectory.Create())
            using (var workingPath = TestDirectory.Create())
            {
                var projectDirectory = Path.Combine(solutionDirectory, "proj1");
                var packagesDirectory = Path.Combine(solutionDirectory, "packages");

                var a1 = new PackageIdentity("A", new NuGetVersion("1.0.0"));
                var a2 = new PackageIdentity("A", new NuGetVersion("2.0.0"));

                var a1Package = Util.CreateTestPackage(
                    a1.Id,
                    a1.Version.ToString(),
                    packagesSourceDirectory,
                    licenseUrl: null,
                    contentFiles: "test1.txt");

                var a2Package = Util.CreateTestPackage(
                    a2.Id,
                    a2.Version.ToString(),
                    packagesSourceDirectory,
                    licenseUrl: null,
                    contentFiles: "test2.txt");

                var packagesFolder = PathUtility.GetRelativePath(projectDirectory, packagesDirectory);

                Directory.CreateDirectory(projectDirectory);
                // create project 1
                Util.CreateFile(
                    projectDirectory,
                    "proj1.csproj",
                    Util.CreateProjFileContent());

                Util.CreateFile(solutionDirectory, "a.sln",
                    Util.CreateSolutionFileContent());

                var projectFile = Path.Combine(projectDirectory, "proj1.csproj");
                var solutionFile = Path.Combine(solutionDirectory, "a.sln");

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msbuildDirectory = MsBuildUtility.GetMsBuildDirectory("14.0", null);
                var projectSystem = new MSBuildProjectSystem(msbuildDirectory, projectFile, testNuGetProjectContext);
                var msBuildProject = new MSBuildNuGetProject(projectSystem, packagesDirectory, projectDirectory);
                using (var stream = File.OpenRead(a1Package))
                {
                    var downloadResult = new DownloadResourceResult(stream);
                    await msBuildProject.InstallPackageAsync(
                        a1,
                        downloadResult,
                        testNuGetProjectContext,
                        CancellationToken.None);
                }

                // Since, content files do not get added by MSBuildProjectSystem used by nuget.exe only,
                // the csproj file will not have the entries for content files.
                // Note that MSBuildProjectSystem used by nuget.exe only, can add or remove references from csproj.
                // Replace proj1.csproj with content files on it, like Install-Package from VS would have.
                File.Delete(projectFile);
                File.WriteAllText(projectFile,
                    Util.CreateProjFileContent(
                        "proj1",
                        "v4.5",
                        references: null,
                        contentFiles: new[] { "test1.txt" }));

                var args = new[]
                {
                    "update",
                    solutionFile,
                    "-Source",
                    packagesSourceDirectory
                };

                var test1textPath = Path.Combine(projectDirectory, "test1.txt");
                var test2textPath = Path.Combine(projectDirectory, "test2.txt");
                var packagesConfigPath = Path.Combine(projectDirectory, "packages.config");

                Assert.True(File.Exists(test1textPath), "Content file test1.txt should exist but does not.");
                Assert.False(File.Exists(test2textPath), "Content file test2.txt should not exist but does.");
                using (var packagesConfigStream = File.OpenRead(packagesConfigPath))
                {
                    var packagesConfigReader = new PackagesConfigReader(packagesConfigStream);
                    var packages = packagesConfigReader.GetPackages().ToList();
                    Assert.Equal(1, packages.Count);
                    Assert.Equal(a1, packages[0].PackageIdentity);
                }

                // Act
                var r = CommandRunner.Run(
                    Util.GetNuGetExePath(),
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.True(r.Item1 == 0, "Output is " + r.Item2 + ". Error is " + r.Item3);
                Assert.False(File.Exists(test1textPath), "Content file test1.txt should not exist but does.");
                Assert.True(File.Exists(test2textPath), "Content file test2.txt should exist but does not.");

                using (var packagesConfigStream = File.OpenRead(packagesConfigPath))
                {
                    var packagesConfigReader = new PackagesConfigReader(packagesConfigStream);
                    var packages = packagesConfigReader.GetPackages().ToList();
                    Assert.Equal(1, packages.Count);
                    Assert.Equal(a2, packages[0].PackageIdentity);
                }
            }
        }

        [Fact]
        public async Task UpdateCommand_Success_CustomPackagesFolder_RelativePath()
        {
            // Arrange
            using (var packagesSourceDirectory = TestDirectory.Create())
            using (var solutionDirectory = TestDirectory.Create())
            {
                // Use a different folder name instead of 'packages'
                var packagesDirectory = Path.Combine(solutionDirectory, "custom-pcks");

                // Create a couple of packages
                var a1 = new PackageIdentity("A", new NuGetVersion("1.0.0"));
                var a2 = new PackageIdentity("A", new NuGetVersion("2.0.0"));

                var a1Package = Util.CreateTestPackage(
                    a1.Id,
                    a1.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });

                var a2Package = Util.CreateTestPackage(
                    a2.Id,
                    a2.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });

                // Create a nuget.config file with a relative 'repositoryPath' setting
                Util.CreateNuGetConfig(solutionDirectory, new[] { packagesSourceDirectory.Path }.ToList(), @".\custom-pcks");

                var projectDirectory = Path.Combine(solutionDirectory, "proj1");
                Directory.CreateDirectory(projectDirectory);
                // create project 1
                Util.CreateFile(
                    projectDirectory,
                    "proj1.csproj",
                    Util.CreateProjFileContent());

                Util.CreateFile(solutionDirectory, "a.sln",
                    Util.CreateSolutionFileContent());

                var projectFile = Path.Combine(projectDirectory, "proj1.csproj");
                var solutionFile = Path.Combine(solutionDirectory, "a.sln");

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msbuildDirectory = MsBuildUtility.GetMsBuildDirectory("14.0", null);
                var projectSystem1 = new MSBuildProjectSystem(msbuildDirectory, projectFile, testNuGetProjectContext);
                var msBuildProject1 = new MSBuildNuGetProject(projectSystem1, packagesDirectory, projectDirectory);
                using (var stream = File.OpenRead(a1Package))
                {
                    var downloadResult = new DownloadResourceResult(stream);
                    await msBuildProject1.InstallPackageAsync(
                        a1,
                        downloadResult,
                        testNuGetProjectContext,
                        CancellationToken.None);
                }

                // The install should create custom the packages folder
                Assert.True(Directory.Exists(packagesDirectory));

                // Run the update command on the solution, which should update package 'A' to v2 into the custom packages folder.
                var args = new[]
                {
                    "update",
                    solutionFile,
                    "-Source",
                    packagesSourceDirectory
                };

                var r = CommandRunner.Run(
                    Util.GetNuGetExePath(),
                    solutionDirectory,
                    string.Join(" ", args),
                    waitForExit: true);

                // Should be no errors returned - used to fail as update command assumed folder was <solutiondir>\packages.
                Assert.Empty(r.Item3);

                // Check that the new version is installed into the custom folder
                Assert.True(Directory.Exists(Path.Combine(packagesDirectory, "A.2.0.0")));

                // Check the custom package folder is used in the assembly reference
                var content1 = File.ReadAllText(projectFile);
                Assert.False(content1.Contains(@"<HintPath>..\custom-pcks\A.1.0.0\lib\net45\file.dll</HintPath>"));
                Assert.True(content1.Contains(@"<HintPath>..\custom-pcks\A.2.0.0\lib\net45\file.dll</HintPath>"));
            }
        }


        [Fact]
        public async Task UpdateCommand_Success_CustomPackagesFolder_AbsolutePath()
        {
            // Arrange
            using (var packagesSourceDirectory = TestDirectory.Create())
            using (var solutionDirectory = TestDirectory.Create())
            using (var packagesDirectory = TestDirectory.Create())
            {
                // Create some packages
                var a1 = new PackageIdentity("A", new NuGetVersion("1.0.0"));
                var a2 = new PackageIdentity("A", new NuGetVersion("2.0.0"));

                var a1Package = Util.CreateTestPackage(
                    a1.Id,
                    a1.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });

                var a2Package = Util.CreateTestPackage(
                    a2.Id,
                    a2.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { NuGetFramework.Parse("net45") },
                    new List<PackageDependencyGroup>() { });

                var projectDirectory = Path.Combine(solutionDirectory, "proj1");
                // Create a nuget.config file that has the full absolute path to 'packagesDirectory'
                Util.CreateNuGetConfig(solutionDirectory, new[] { packagesSourceDirectory.Path }.ToList(), packagesDirectory);

                Directory.CreateDirectory(projectDirectory);
                // create project 1
                Util.CreateFile(
                    projectDirectory,
                    "proj1.csproj",
                    Util.CreateProjFileContent());

                Util.CreateFile(solutionDirectory, "a.sln",
                    Util.CreateSolutionFileContent());

                var projectFile = Path.Combine(projectDirectory, "proj1.csproj");
                var solutionFile = Path.Combine(solutionDirectory, "a.sln");

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msbuildDirectory = MsBuildUtility.GetMsBuildDirectory("14.0", null);
                var projectSystem1 = new MSBuildProjectSystem(msbuildDirectory, projectFile, testNuGetProjectContext);
                var msBuildProject1 = new MSBuildNuGetProject(projectSystem1, packagesDirectory, projectDirectory);
                using (var stream = File.OpenRead(a1Package))
                {
                    var downloadResult = new DownloadResourceResult(stream);
                    await msBuildProject1.InstallPackageAsync(
                        a1,
                        downloadResult,
                        testNuGetProjectContext,
                        CancellationToken.None);
                }

                // The install should create custom the packages folder
                Assert.True(Directory.Exists(packagesDirectory));

                var args = new[]
                {
                    "update",
                    solutionFile,
                    "-Source",
                    packagesSourceDirectory
                };

                // Run the update command on the solution
                var r = CommandRunner.Run(
                    Util.GetNuGetExePath(),
                    solutionDirectory,
                    string.Join(" ", args),
                    waitForExit: true);

                // Should be no errors returned - used to fail as update command assumed folder was <solutiondir>\packages.
                Assert.Empty(r.Item3);

                // Check that the new version is installed into the custom folder
                Assert.True(Directory.Exists(Path.Combine(packagesDirectory, "A.2.0.0")));

                // Check the custom package folder is used in the assembly reference
                var content1 = File.ReadAllText(projectFile);
                var customPackageFolderName = new DirectoryInfo(packagesDirectory.Path).Name;
                Assert.False(content1.Contains(@"<HintPath>..\..\" + customPackageFolderName + @"\A.1.0.0\lib\net45\file.dll</HintPath>"));
                Assert.True(content1.Contains(@"<HintPath>..\..\" + customPackageFolderName + @"\A.2.0.0\lib\net45\file.dll</HintPath>"));
            }
        }

        [Fact]
        public async Task UpdateCommand_Native_JS_Projects_Success()
        {
            using (var packagesSourceDirectory = TestDirectory.Create())
            using (var solutionDirectory = TestDirectory.Create())
            using (var workingPath = TestDirectory.Create())
            {
                var projectDirectory1 = Path.Combine(solutionDirectory, "proj1");
                var projectDirectory2 = Path.Combine(solutionDirectory, "proj2");
                var packagesDirectory = Path.Combine(solutionDirectory, "packages");

                var a1 = new PackageIdentity("A", new NuGetVersion("1.0.0"));
                var a2 = new PackageIdentity("A", new NuGetVersion("2.0.0"));

                var b1 = new PackageIdentity("B", new NuGetVersion("1.0.0"));
                var b2 = new PackageIdentity("B", new NuGetVersion("2.0.0"));

                var a1Package = Util.CreateTestPackage(
                    a1.Id,
                    a1.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { },
                    "test.txt");

                var a2Package = Util.CreateTestPackage(
                    a2.Id,
                    a2.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { },
                    "test.txt");

                var b1Package = Util.CreateTestPackage(
                    b1.Id,
                    b1.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { },
                    "test.txt");

                var b2Package = Util.CreateTestPackage(
                    b2.Id,
                    b2.Version.ToString(),
                    packagesSourceDirectory,
                    new List<NuGetFramework>() { },
                    "test.txt");

                Directory.CreateDirectory(projectDirectory1);

                Util.CreateFile(
                    projectDirectory1,
                    "proj1.jsproj",
                    Util.CreateProjFileContent());

                Directory.CreateDirectory(projectDirectory2);

                Util.CreateFile(
                    projectDirectory2,
                    "proj2.vcxproj",
                    Util.CreateProjFileContent());

                Util.CreateFile(solutionDirectory, "a.sln",
                    Util.CreateSolutionFileContent());

                var projectFile1 = Path.Combine(projectDirectory1, "proj1.jsproj");
                var projectFile2 = Path.Combine(projectDirectory2, "proj2.vcxproj");
                var solutionFile = Path.Combine(solutionDirectory, "a.sln");

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msbuildDirectory = MsBuildUtility.GetMsBuildDirectory("14.0", null);
                var projectSystem1 = new MSBuildProjectSystem(msbuildDirectory, projectFile1, testNuGetProjectContext);
                var projectSystem2 = new MSBuildProjectSystem(msbuildDirectory, projectFile2, testNuGetProjectContext);
                var msBuildProject1 = new MSBuildNuGetProject(projectSystem1, packagesDirectory, projectDirectory1);
                var msBuildProject2 = new MSBuildNuGetProject(projectSystem2, packagesDirectory, projectDirectory2);

                using (var stream = File.OpenRead(a1Package))
                {
                    var downloadResult = new DownloadResourceResult(stream);
                    await msBuildProject1.InstallPackageAsync(
                        a1,
                        downloadResult,
                        testNuGetProjectContext,
                        CancellationToken.None);
                }

                using (var stream = File.OpenRead(b1Package))
                {
                    var downloadResult = new DownloadResourceResult(stream);
                    await msBuildProject2.InstallPackageAsync(
                        b1,
                        downloadResult,
                        testNuGetProjectContext,
                        CancellationToken.None);
                }

                projectSystem1.Save();
                projectSystem2.Save();

                var args = new[]
                {
                    "update",
                    solutionFile,
                    "-Source",
                    packagesSourceDirectory
                };

                var r = CommandRunner.Run(
                    Util.GetNuGetExePath(),
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                Assert.True(r.Item1 == 0, "Output is " + r.Item2 + ". Error is " + r.Item3);
            }
        }
    }
}
