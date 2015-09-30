using System.Collections.Generic;
using System.IO;
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
    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include='proj1_file1.cs' />
  </ItemGroup>
  <ItemGroup>
    <Content Include='proj1_file2.txt' />
  </ItemGroup>
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
</Project>");

                Util.CreateFile(solutionDirectory, "a.sln",
                    @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj1"", ""proj1.csproj"", ""{A04C59CC-7622-4223-B16B-CDF2ECAD438D}""
EndProject");

                var projectFile = Path.Combine(projectDirectory, "proj1.csproj");
                var solutionFile = Path.Combine(solutionDirectory, "a.sln");

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msbuildDirectory = MsBuildUtility.GetMsbuildDirectory("14.0", null);
                var projectSystem = new MSBuildProjectSystem(msbuildDirectory, projectFile, testNuGetProjectContext);
                var msBuildProject = new MSBuildNuGetProject(projectSystem, packagesDirectory, projectDirectory);
                using (var stream = File.OpenRead(a1Package))
                {
                    var downloadResult = new DownloadResourceResult(stream);
                    await msBuildProject.InstallPackageAsync(a1, downloadResult, testNuGetProjectContext, CancellationToken.None);
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
    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include='proj1_file1.cs' />
  </ItemGroup>
  <ItemGroup>
    <Content Include='proj1_file2.txt' />
  </ItemGroup>
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
</Project>");

                Util.CreateFile(solutionDirectory, "a.sln",
                    @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj1"", ""proj1.csproj"", ""{A04C59CC-7622-4223-B16B-CDF2ECAD438D}""
EndProject");

                var projectFile = Path.Combine(projectDirectory, "proj1.csproj");
                var solutionFile = Path.Combine(solutionDirectory, "a.sln");

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msbuildDirectory = MsBuildUtility.GetMsbuildDirectory("14.0", null);
                var projectSystem = new MSBuildProjectSystem(msbuildDirectory, projectFile, testNuGetProjectContext);
                var msBuildProject = new MSBuildNuGetProject(projectSystem, packagesDirectory, projectDirectory);
                using (var stream = File.OpenRead(a1Package))
                {
                    var downloadResult = new DownloadResourceResult(stream);
                    await msBuildProject.InstallPackageAsync(a1, downloadResult, testNuGetProjectContext, CancellationToken.None);
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
    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include='proj1_file1.cs' />
  </ItemGroup>
  <ItemGroup>
    <Content Include='proj1_file2.txt' />
  </ItemGroup>
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
</Project>");

                Util.CreateFile(solutionDirectory, "a.sln",
                    @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj1"", ""proj1.csproj"", ""{A04C59CC-7622-4223-B16B-CDF2ECAD438D}""
EndProject");

                var projectFile = Path.Combine(projectDirectory, "proj1.csproj");
                var solutionFile = Path.Combine(solutionDirectory, "a.sln");

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msbuildDirectory = MsBuildUtility.GetMsbuildDirectory("14.0", null);
                var projectSystem = new MSBuildProjectSystem(msbuildDirectory, projectFile, testNuGetProjectContext);
                var msBuildProject = new MSBuildNuGetProject(projectSystem, packagesDirectory, projectDirectory);
                using (var stream = File.OpenRead(a1Package))
                {
                    var downloadResult = new DownloadResourceResult(stream);
                    await msBuildProject.InstallPackageAsync(a1, downloadResult, testNuGetProjectContext, CancellationToken.None);
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
    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include='proj1_file1.cs' />
  </ItemGroup>
  <ItemGroup>
    <Content Include='proj1_file2.txt' />
  </ItemGroup>
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
</Project>");

                Util.CreateFile(solutionDirectory, "a.sln",
                    @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj1"", ""proj1.csproj"", ""{A04C59CC-7622-4223-B16B-CDF2ECAD438D}""
EndProject");

                var projectFile = Path.Combine(projectDirectory, "proj1.csproj");
                var solutionFile = Path.Combine(solutionDirectory, "a.sln");

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msbuildDirectory = MsBuildUtility.GetMsbuildDirectory("14.0", null);
                var projectSystem = new MSBuildProjectSystem(msbuildDirectory, projectFile, testNuGetProjectContext);
                var msBuildProject = new MSBuildNuGetProject(projectSystem, packagesDirectory, projectDirectory);
                using (var stream = File.OpenRead(a1Package))
                {
                    var downloadResult = new DownloadResourceResult(stream);
                    await msBuildProject.InstallPackageAsync(a1, downloadResult, testNuGetProjectContext, CancellationToken.None);
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
    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include='proj1_file1.cs' />
  </ItemGroup>
  <ItemGroup>
    <Content Include='proj1_file2.txt' />
  </ItemGroup>
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
</Project>");

                Util.CreateFile(solutionDirectory, "a.sln",
                    @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj1"", ""proj1.csproj"", ""{A04C59CC-7622-4223-B16B-CDF2ECAD438D}""
EndProject");

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
                    await msBuildProject.InstallPackageAsync(a1, downloadResult, testNuGetProjectContext, CancellationToken.None);
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
    }
}
