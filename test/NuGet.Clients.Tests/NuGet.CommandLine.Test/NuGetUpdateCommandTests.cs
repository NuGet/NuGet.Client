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
        public async Task UpdateCommand_Success_References()
        {
            using (var packagesSourceDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var solutionDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
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
                var msbuildDirectory = MsBuildUtility.GetMsbuildDirectory("14.0", null);
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

            using (var packagesSourceDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var solutionDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
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
                var msbuildDirectory = MsBuildUtility.GetMsbuildDirectory("14.0", null);
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
        public async Task UpdateCommand_Success_References_MultipleProjects()
        {
            string packagesSourceDirectory = null;
            string solutionDirectory = null;
            string workingPath = null;

            try
            {
                packagesSourceDirectory = TestFilesystemUtility.CreateRandomTestFolder();
                solutionDirectory = TestFilesystemUtility.CreateRandomTestFolder();
                workingPath = TestFilesystemUtility.CreateRandomTestFolder();

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
                var msbuildDirectory = MsBuildUtility.GetMsbuildDirectory("14.0", null);
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
            finally
            {
                TestFilesystemUtility.DeleteRandomTestFolders(solutionDirectory, workingPath, packagesSourceDirectory);
            }
        }

        [Fact]
        public async Task UpdateCommand_Success_NOPrerelease()
        {
            using (var packagesSourceDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var solutionDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
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
                var msbuildDirectory = MsBuildUtility.GetMsbuildDirectory("14.0", null);
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
            using (var packagesSourceDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var solutionDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
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
                var msbuildDirectory = MsBuildUtility.GetMsbuildDirectory("14.0", null);
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
                Assert.True(content.Contains(@"<HintPath>..\packages\A.2.0.0-beta\lib\net45\file.dll</HintPath>"));
            }
        }

        [Fact]
        public async Task UpdateCommand_Success_ProjectFile_References()
        {
            using (var packagesSourceDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var solutionDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
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
                var msbuildDirectory = MsBuildUtility.GetMsbuildDirectory("14.0", null);
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
            using (var packagesSourceDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var solutionDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
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
                var msbuildDirectory = MsBuildUtility.GetMsbuildDirectory("14.0", null);
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
            using (var packagesSourceDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var solutionDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
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
                var msbuildDirectory = MsBuildUtility.GetMsbuildDirectory("14.0", null);
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
    }
}
