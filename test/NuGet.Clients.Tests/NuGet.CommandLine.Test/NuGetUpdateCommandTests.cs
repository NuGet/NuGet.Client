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
            var packagesSourceDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var solutionDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var projectDirectory = Path.Combine(solutionDirectory, "proj1");
            var packagesDirectory = Path.Combine(solutionDirectory, "packages");
            var workingPath = TestFilesystemUtility.CreateRandomTestFolder();

            try
            {
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
            finally
            {
                TestFilesystemUtility.DeleteRandomTestFolders(solutionDirectory, workingPath, packagesSourceDirectory);
            }
        }

        [Fact]
        public async Task UpdateCommand_Success_NOPrerelease()
        {
            var packagesSourceDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var solutionDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var projectDirectory = Path.Combine(solutionDirectory, "proj1");
            var packagesDirectory = Path.Combine(solutionDirectory, "packages");
            var workingPath = TestFilesystemUtility.CreateRandomTestFolder();

            try
            {
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
            finally
            {
                TestFilesystemUtility.DeleteRandomTestFolders(solutionDirectory, workingPath, packagesSourceDirectory);
            }
        }

        [Fact]
        public async Task UpdateCommand_Success_Prerelease()
        {
            var packagesSourceDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var solutionDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var projectDirectory = Path.Combine(solutionDirectory, "proj1");
            var packagesDirectory = Path.Combine(solutionDirectory, "packages");
            var workingPath = TestFilesystemUtility.CreateRandomTestFolder();

            try
            {
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
            finally
            {
                TestFilesystemUtility.DeleteRandomTestFolders(solutionDirectory, workingPath, packagesSourceDirectory);
            }
        }

        [Fact]
        public async Task UpdateCommand_Success_ProjectFile_References()
        {
            var packagesSourceDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var solutionDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var projectDirectory = Path.Combine(solutionDirectory, "proj1");
            var packagesDirectory = Path.Combine(solutionDirectory, "packages");
            var workingPath = TestFilesystemUtility.CreateRandomTestFolder();

            try
            {
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
            finally
            {
                TestFilesystemUtility.DeleteRandomTestFolders(solutionDirectory, workingPath, packagesSourceDirectory);
            }
        }

        [Fact]
        public async Task UpdateCommand_Success_PackagesConfig_References()
        {
            var packagesSourceDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var solutionDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var projectDirectory = Path.Combine(solutionDirectory, "proj1");
            var packagesDirectory = Path.Combine(solutionDirectory, "packages");
            var workingPath = TestFilesystemUtility.CreateRandomTestFolder();

            try
            {
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
            finally
            {
                TestFilesystemUtility.DeleteRandomTestFolders(solutionDirectory, workingPath, packagesSourceDirectory);
            }
        }

        [Fact]
        public async Task UpdateCommand_Success_ContentFiles()
        {
            // Arrange
            var packagesSourceDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var solutionDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var projectDirectory = Path.Combine(solutionDirectory, "proj1");
            var packagesDirectory = Path.Combine(solutionDirectory, "packages");
            var workingPath = TestFilesystemUtility.CreateRandomTestFolder();

            try
            {
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
            finally
            {
                TestFilesystemUtility.DeleteRandomTestFolders(solutionDirectory, workingPath, packagesSourceDirectory);
            }
        }
    }
}
