using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetRestoreCommandTest
    {
        [Fact]
        public void RestoreCommand_BadInputPath()
        {
            using (var randomTestFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var nugetexe = Util.GetNuGetExePath();
                var solutionPath = "bad/pat.h/myfile.blah";

                var args = new string[]
                {
                    "restore",
                    solutionPath,
                    "-PackagesDirectory",
                    randomTestFolder
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.NotEqual(0, r.Item1);
                var error = r.Item3;
                Assert.Contains("could not find a part of the path", r.Item3, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void RestoreCommand_MissingSolutionFile()
        {
            using (var randomTestFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var nugetexe = Util.GetNuGetExePath();
                var solutionPath = Path.Combine(randomTestFolder, "solution.sln");

                var args = new string[]
                {
                    "restore",
                    solutionPath,
                    "-PackagesDirectory",
                    randomTestFolder
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.NotEqual(0, r.Item1);
                var error = r.Item3;
                Assert.Contains("could not find a part of the path", r.Item3, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void TestVerbosityQuiet_ShowsErrorMessages()
        {
            using (var randomTestFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var nugetexe = Util.GetNuGetExePath();
                var solutionPath = Path.Combine(randomTestFolder, "solution.sln");

                var args = new string[]
                {
                    "restore",
                    solutionPath,
                    "-PackagesDirectory",
                    randomTestFolder,
                    "-Verbosity",
                    "Quiet"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.NotEqual(0, r.Item1);
                var error = r.Item3;
                Assert.Contains("could not find a part of the path", r.Item3, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void RestoreCommand_MissingPackagesConfigFile()
        {
            using (var randomTestFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var nugetexe = Util.GetNuGetExePath();
                var packagesConfigPath = Path.Combine(randomTestFolder, "packages.config");

                var args = new string[]
                {
                    "restore",
                    packagesConfigPath,
                    "-PackagesDirectory",
                    randomTestFolder
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.NotEqual(0, r.Item1);
                var error = r.Item3;
                Assert.Contains("input file does not exist", r.Item3, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void RestoreCommand_FromPackagesConfigFile()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                Directory.CreateDirectory(repositoryPath);
                Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
                Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);
                Util.CreateFile(workingPath, "packages.config",
@"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
  <package id=""packageB"" version=""2.2.0"" targetFramework=""net45"" />
</packages>");

                string[] args = new string[] { "restore", "-PackagesDirectory", "outputDir", "-Source", repositoryPath };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                var packageFileA = Path.Combine(workingPath, @"outputDir\packageA.1.1.0\packageA.1.1.0.nupkg");
                var packageFileB = Path.Combine(workingPath, @"outputDir\packageB.2.2.0\packageB.2.2.0.nupkg");
                Assert.True(File.Exists(packageFileA));
                Assert.True(File.Exists(packageFileB));
            }
        }

        [Theory]
        [InlineData("packages.config")]
        [InlineData("packages.proj2.config")]
        public void RestoreCommand_FromSolutionFile(string configFileName)
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var proj1Directory = Path.Combine(workingPath, "proj1");
                var proj2Directory = Path.Combine(workingPath, "proj2");

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(proj1Directory);
                Directory.CreateDirectory(proj2Directory);

                Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
                Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);

                Util.CreateFile(workingPath, "a.sln",
                    @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj1"", ""proj1\proj1.csproj"", ""{A04C59CC-7622-4223-B16B-CDF2ECAD438D}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj2"", ""proj2\proj2.csproj"", ""{42641DAE-D6C4-49D4-92EA-749D2573554A}""
EndProject");

                Util.CreateFile(proj1Directory, "proj1.csproj",
                    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");
                Util.CreateFile(proj1Directory, "packages.config",
@"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
</packages>");

                Util.CreateFile(proj2Directory, "proj2.csproj",
                    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");
                Util.CreateFile(proj2Directory, configFileName,
@"<packages>
  <package id=""packageB"" version=""2.2.0"" targetFramework=""net45"" />
</packages>");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    "restore -Source " + repositoryPath,
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                var packageFileA = Path.Combine(workingPath, @"packages\packageA.1.1.0\packageA.1.1.0.nupkg");
                var packageFileB = Path.Combine(workingPath, @"packages\packageB.2.2.0\packageB.2.2.0.nupkg");
                Assert.True(File.Exists(packageFileA));
                Assert.True(File.Exists(packageFileB));
            }
        }

        [Fact]
        public void RestoreCommand_FromProjectFile()
        {
            // Arrange
            using (var repositoryPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var proj1Directory = Path.Combine(workingPath, "proj1");
                Directory.CreateDirectory(proj1Directory);

                var currentDirectory = Directory.GetCurrentDirectory();
                var nugetexe = Util.GetNuGetExePath();

                Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
                Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);

                var proj1File = Path.Combine(proj1Directory, "proj1.csproj");
                File.WriteAllText(
                    proj1File,
                    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");

                File.WriteAllText(
                    Path.Combine(proj1Directory, "packages.config"),
@"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
  <package id=""packageB"" version=""2.2.0"" targetFramework=""net45"" />
</packages>");

                string[] args = new string[]
                    {
                        "restore",
                        proj1File,
                        "-Source",
                        repositoryPath,
                        "-solutionDir",
                        workingPath
                    };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                var packageFileA = Path.Combine(workingPath, @"packages\packageA.1.1.0\packageA.1.1.0.nupkg");
                var packageFileB = Path.Combine(workingPath, @"packages\packageB.2.2.0\packageB.2.2.0.nupkg");
                Assert.True(File.Exists(packageFileA));
                Assert.True(File.Exists(packageFileB));
            }
        }

        [Theory]
        [InlineData("packages.config")]
        [InlineData("packages.proj2.config")]
        public void RestoreCommand_FromSolutionFileWithMsbuild12(string configFileName)
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var proj1Directory = Path.Combine(workingPath, "proj1");
                var proj2Directory = Path.Combine(workingPath, "proj2");

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(proj1Directory);
                Directory.CreateDirectory(proj2Directory);

                Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
                Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);

                Util.CreateFile(workingPath, "a.sln",
                    @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj1"", ""proj1\proj1.csproj"", ""{A04C59CC-7622-4223-B16B-CDF2ECAD438D}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj2"", ""proj2\proj2.csproj"", ""{42641DAE-D6C4-49D4-92EA-749D2573554A}""
EndProject");

                Util.CreateFile(proj1Directory, "proj1.csproj",
                    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");
                Util.CreateFile(proj1Directory, "packages.config",
@"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
</packages>");

                Util.CreateFile(proj2Directory, "proj2.csproj",
                    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");
                Util.CreateFile(proj2Directory, configFileName,
@"<packages>
  <package id=""packageB"" version=""2.2.0"" targetFramework=""net45"" />
</packages>");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    "restore -Source " + repositoryPath + @" -msbuildversion 12",
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                var packageFileA = Path.Combine(workingPath, @"packages\packageA.1.1.0\packageA.1.1.0.nupkg");
                var packageFileB = Path.Combine(workingPath, @"packages\packageB.2.2.0\packageB.2.2.0.nupkg");
                Assert.True(File.Exists(packageFileA));
                Assert.True(File.Exists(packageFileB));
            }
        }

        [Theory]
        [InlineData("packages.config")]
        [InlineData("packages.proj2.config")]
        public void RestoreCommand_FromSolutionFileWithMsbuild14(string configFileName)
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var proj1Directory = Path.Combine(workingPath, "proj1");
                var proj2Directory = Path.Combine(workingPath, "proj2");

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(proj1Directory);
                Directory.CreateDirectory(proj2Directory);

                Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
                Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);

                Util.CreateFile(workingPath, "a.sln",
                    @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj1"", ""proj1\proj1.csproj"", ""{A04C59CC-7622-4223-B16B-CDF2ECAD438D}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj2"", ""proj2\proj2.csproj"", ""{42641DAE-D6C4-49D4-92EA-749D2573554A}""
EndProject");

                Util.CreateFile(proj1Directory, "proj1.csproj",
                    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");
                Util.CreateFile(proj1Directory, "packages.config",
@"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
</packages>");

                Util.CreateFile(proj2Directory, "proj2.csproj",
                    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");
                Util.CreateFile(proj2Directory, configFileName,
@"<packages>
  <package id=""packageB"" version=""2.2.0"" targetFramework=""net45"" />
</packages>");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    "restore -Source " + repositoryPath + @" -MSBuildVersion 14.0",
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                var packageFileA = Path.Combine(workingPath, @"packages\packageA.1.1.0\packageA.1.1.0.nupkg");
                var packageFileB = Path.Combine(workingPath, @"packages\packageB.2.2.0\packageB.2.2.0.nupkg");
                Assert.True(File.Exists(packageFileA));
                Assert.True(File.Exists(packageFileB));
            }
        }

        // Tests that if the project file cannot be loaded, i.e. InvalidProjectFileException is thrown,
        // Then packages listed in packages.config file will be restored.
        [Fact]
        public void RestoreCommand_ProjectCannotBeLoaded()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var proj1Directory = Path.Combine(workingPath, "proj1");

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(proj1Directory);

                Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);

                Util.CreateFile(workingPath, "a.sln",
                    @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj1"", ""proj1\proj1.csproj"", ""{A04C59CC-7622-4223-B16B-CDF2ECAD438D}""
EndProject");

                // The project contains an import statement to import an non-existing file.
                // Thus, this project cannot be loaded successfully.
                Util.CreateFile(proj1Directory, "proj1.csproj",
                    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <Import Project='..\packages\a.targets' />
</Project>");
                Util.CreateFile(proj1Directory, "packages.config",
@"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
</packages>");
                string[] args = new string[] { "restore", "-Source", repositoryPath };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    "restore -Source " + repositoryPath,
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                var packageFileA = Path.Combine(workingPath, @"packages\packageA.1.1.0\packageA.1.1.0.nupkg");
                Assert.True(File.Exists(packageFileA));
            }
        }

        // Tests that when -solutionDir is specified, the $(SolutionDir)\.nuget\NuGet.Config file
        // will be used.
        [Fact]
        public void RestoreCommand_FromPackagesConfigFileWithOptionSolutionDir()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");

                Directory.CreateDirectory(repositoryPath);

                Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
                Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);
                Util.CreateFile(workingPath, "packages.config",
@"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
  <package id=""packageB"" version=""2.2.0"" targetFramework=""net45"" />
</packages>");
                Util.CreateFile(Path.Combine(workingPath, ".nuget"), "nuget.config",
@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <config>
    <add key=""repositorypath"" value=""$\..\..\Packages2"" />
  </config>
</configuration>");

                string[] args = new string[] { "restore", "-Source", repositoryPath, "-solutionDir", workingPath };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                var packageFileA = Path.Combine(workingPath, @"packages2\packageA.1.1.0\packageA.1.1.0.nupkg");
                var packageFileB = Path.Combine(workingPath, @"packages2\packageB.2.2.0\packageB.2.2.0.nupkg");
                Assert.True(File.Exists(packageFileA));
                Assert.True(File.Exists(packageFileB));
            }
        }

        // Tests that when package restore is enabled and -RequireConsent is specified,
        // the opt out message is displayed.
        // TODO: renable the test once this is implemented
        // [Theory]
        [InlineData("packages.config")]
        [InlineData("packages.proj1.config")]
        public void RestoreCommand_OptOutMessage(string configFileName)
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var proj1Directory = Path.Combine(workingPath, "proj1");
                var proj2Directory = Path.Combine(workingPath, "proj2");

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(proj1Directory);
                Directory.CreateDirectory(proj2Directory);

                Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
                Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);

                Util.CreateFile(workingPath, "a.sln",
                    @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj1"", ""proj1\proj1.csproj"", ""{A04C59CC-7622-4223-B16B-CDF2ECAD438D}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj2"", ""proj2\proj2.csproj"", ""{42641DAE-D6C4-49D4-92EA-749D2573554A}""
EndProject");
                Util.CreateFile(workingPath, "my.config",
                    @"
<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageRestore>
    <add key=""enabled"" value=""True"" />
  </packageRestore>
</configuration>");

                Util.CreateFile(proj1Directory, "proj1.csproj",
                    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");
                Util.CreateFile(proj1Directory, configFileName,
@"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
</packages>");

                Util.CreateFile(proj2Directory, "proj2.csproj",
                    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");
                Util.CreateFile(proj2Directory, "packages.config",
@"<packages>
  <package id=""packageB"" version=""2.2.0"" targetFramework=""net45"" />
</packages>");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    "restore -Source " + repositoryPath + " -ConfigFile my.config -RequireConsent",
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                string optOutMessage = String.Format(
                    CultureInfo.CurrentCulture,
                    NuGetResources.RestoreCommandPackageRestoreOptOutMessage,
                    NuGet.Resources.NuGetResources.PackageRestoreConsentCheckBoxText.Replace("&", ""));
                Assert.Contains(optOutMessage, r.Item2);
                var packageFileA = Path.Combine(workingPath, @"packages\packageA.1.1.0\packageA.1.1.0.nupkg");
                var packageFileB = Path.Combine(workingPath, @"packages\packageB.2.2.0\packageB.2.2.0.nupkg");
                Assert.True(File.Exists(packageFileA));
                Assert.True(File.Exists(packageFileB));
            }
        }

        // Tests that when package restore is enabled and -RequireConsent is not specified,
        // the opt out message is not displayed.
        [Fact]
        public void RestoreCommand_NoOptOutMessage()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var proj1Directory = Path.Combine(workingPath, "proj1");
                var proj2Directory = Path.Combine(workingPath, "proj2");

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(proj1Directory);
                Directory.CreateDirectory(proj2Directory);

                Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
                Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);

                Util.CreateFile(workingPath, "a.sln",
                    @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj1"", ""proj1\proj1.csproj"", ""{A04C59CC-7622-4223-B16B-CDF2ECAD438D}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj2"", ""proj2\proj2.csproj"", ""{42641DAE-D6C4-49D4-92EA-749D2573554A}""
EndProject");
                Util.CreateFile(workingPath, "my.config",
                    @"
<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageRestore>
    <add key=""enabled"" value=""True"" />
  </packageRestore>
</configuration>");

                Util.CreateFile(proj1Directory, "proj1.csproj",
                    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");
                Util.CreateFile(proj1Directory, "packages.config",
@"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
</packages>");

                Util.CreateFile(proj2Directory, "proj2.csproj",
                    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");
                Util.CreateFile(proj2Directory, "packages.config",
@"<packages>
  <package id=""packageB"" version=""2.2.0"" targetFramework=""net45"" />
</packages>");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    "restore -Source " + repositoryPath + " -ConfigFile my.config",
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                string optOutMessage = String.Format(
                    CultureInfo.CurrentCulture,
                    NuGetResources.RestoreCommandPackageRestoreOptOutMessage,
                    NuGet.Resources.NuGetResources.PackageRestoreConsentCheckBoxText.Replace("&", ""));
                Assert.DoesNotContain(optOutMessage, r.Item2);
                var packageFileA = Path.Combine(workingPath, @"packages\packageA.1.1.0\packageA.1.1.0.nupkg");
                var packageFileB = Path.Combine(workingPath, @"packages\packageB.2.2.0\packageB.2.2.0.nupkg");
                Assert.True(File.Exists(packageFileA));
                Assert.True(File.Exists(packageFileB));
            }
        }

        // Test that when a directory is passed to nuget.exe restore, and the directory contains
        // just one solution file, restore will work on that solution file.
        [Theory]
        [InlineData("packages.config")]
        [InlineData("packages.proj2.config")]
        public void RestoreCommand_OneSolutionFileInDirectory(string configFileName)
        {
            // Arrang
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomTestFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var proj1Directory = Path.Combine(workingPath, "proj1");
                var proj2Directory = Path.Combine(workingPath, "proj2");

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(proj1Directory);
                Directory.CreateDirectory(proj2Directory);

                Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
                Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);

                Util.CreateFile(workingPath, "a.sln",
                    @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj1"", ""proj1\proj1.csproj"", ""{A04C59CC-7622-4223-B16B-CDF2ECAD438D}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj2"", ""proj2\proj2.csproj"", ""{42641DAE-D6C4-49D4-92EA-749D2573554A}""
EndProject");

                Util.CreateFile(proj1Directory, "proj1.csproj",
                    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");
                Util.CreateFile(proj1Directory, "packages.config",
@"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
</packages>");

                Util.CreateFile(proj2Directory, "proj2.csproj",
                    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");
                Util.CreateFile(proj2Directory, configFileName,
@"<packages>
  <package id=""packageB"" version=""2.2.0"" targetFramework=""net45"" />
</packages>");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    randomTestFolder,
                    "restore " + workingPath + " -Source " + repositoryPath,
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                var packageFileA = Path.Combine(workingPath, @"packages\packageA.1.1.0\packageA.1.1.0.nupkg");
                var packageFileB = Path.Combine(workingPath, @"packages\packageB.2.2.0\packageB.2.2.0.nupkg");
                Assert.True(File.Exists(packageFileA));
                Assert.True(File.Exists(packageFileB));
            }
        }

        // Test that when a directory is passed to nuget.exe restore, and the directory contains
        // multiple solution files, nuget.exe will generate an error.
        [Fact]
        public void RestoreCommand_MutipleSolutionFilesInDirectory()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomTestFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var proj1Directory = Path.Combine(workingPath, "proj1");
                var proj2Directory = Path.Combine(workingPath, "proj2");

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(proj1Directory);
                Directory.CreateDirectory(proj2Directory);

                Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
                Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);

                Util.CreateFile(workingPath, "a.sln",
                    @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj1"", ""proj1\proj1.csproj"", ""{A04C59CC-7622-4223-B16B-CDF2ECAD438D}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj2"", ""proj2\proj2.csproj"", ""{42641DAE-D6C4-49D4-92EA-749D2573554A}""
EndProject");
                Util.CreateFile(workingPath, "b.sln",
                    @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj1"", ""proj1\proj1.csproj"", ""{A04C59CC-7622-4223-B16B-CDF2ECAD438D}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj2"", ""proj2\proj2.csproj"", ""{42641DAE-D6C4-49D4-92EA-749D2573554A}""
EndProject");

                Util.CreateFile(proj1Directory, "proj1.csproj",
                    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");
                Util.CreateFile(proj1Directory, "packages.config",
@"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
</packages>");

                Util.CreateFile(proj2Directory, "proj2.csproj",
                    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");
                Util.CreateFile(proj2Directory, "packages.config",
@"<packages>
  <package id=""packageB"" version=""2.2.0"" targetFramework=""net45"" />
</packages>");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    randomTestFolder,
                    "restore " + workingPath + " -Source " + repositoryPath,
                    waitForExit: true);

                // Assert
                Assert.Equal(1, r.Item1);
                Assert.Contains("This folder contains more than one solution file.", r.Item3);
                var packageFileA = Path.Combine(workingPath, @"packages\packageA.1.1.0\packageA.1.1.0.nupkg");
                var packageFileB = Path.Combine(workingPath, @"packages\packageB.2.2.0\packageB.2.2.0.nupkg");
                Assert.False(File.Exists(packageFileA));
                Assert.False(File.Exists(packageFileB));
            }
        }

        // Test that when a directory is passed to nuget.exe restore, and the directory contains
        // no solution files, nuget.exe will generate an error.
        [Fact]
        public void RestoreCommand_NoSolutionFilesInDirectory()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomTestFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var proj1Directory = Path.Combine(workingPath, "proj1");
                var proj2Directory = Path.Combine(workingPath, "proj2");

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(proj1Directory);
                Directory.CreateDirectory(proj2Directory);

                Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
                Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);

                Util.CreateFile(proj1Directory, "proj1.csproj",
                    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");
                Util.CreateFile(proj1Directory, "packages.config",
@"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
</packages>");

                Util.CreateFile(proj2Directory, "proj2.csproj",
                    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");
                Util.CreateFile(proj2Directory, "packages.config",
@"<packages>
  <package id=""packageB"" version=""2.2.0"" targetFramework=""net45"" />
</packages>");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    randomTestFolder,
                    "restore " + workingPath + " -Source " + repositoryPath,
                    waitForExit: true);

                // Assert
                Assert.Equal(1, r.Item1);
                Assert.Contains("Cannot locate a solution file.", r.Item3);
                var packageFileA = Path.Combine(workingPath, @"packages\packageA.1.1.0\packageA.1.1.0.nupkg");
                var packageFileB = Path.Combine(workingPath, @"packages\packageB.2.2.0\packageB.2.2.0.nupkg");
                Assert.False(File.Exists(packageFileA));
                Assert.False(File.Exists(packageFileB));
            }
        }

        // Tests that package restore loads the correct config file when -ConfigFile
        // is specified.
        [Fact]
        public void RestoreCommand_ConfigFile()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var proj1Directory = Path.Combine(workingPath, "proj1");

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(proj1Directory);

                Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);

                Util.CreateFile(workingPath, "a.sln",
                    @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj1"", ""proj1\proj1.csproj"", ""{A04C59CC-7622-4223-B16B-CDF2ECAD438D}""
EndProject");
                Util.CreateFile(workingPath, "my.config",
                    String.Format(CultureInfo.InvariantCulture,
@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""mysouce"" value=""{0}"" />
    </packageSources>
</configuration>", repositoryPath));

                Util.CreateFile(proj1Directory, "proj1.csproj",
                    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");
                Util.CreateFile(proj1Directory, "packages.config",
@"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
</packages>");

                // Act
                // the package source listed in my.config will be used.
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    "restore -ConfigFile my.config",
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                var packageFileA = Path.Combine(workingPath, @"packages\packageA.1.1.0\packageA.1.1.0.nupkg");
                Assert.True(File.Exists(packageFileA));
            }
        }

        // Tests that when -PackageSaveMode is set to nuspec, the nuspec files, instead of
        // nupkg files, are saved.
        [Fact(Skip = "PackageSaveMode is not implemented yet")]
        public void RestoreCommand_PackageSaveModeNuspec()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                var proj1Directory = Path.Combine(workingPath, "proj1");
                var proj2Directory = Path.Combine(workingPath, "proj2");

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(proj1Directory);
                Directory.CreateDirectory(proj2Directory);

                Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
                Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);

                Util.CreateFile(workingPath, "a.sln",
                    @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj1"", ""proj1\proj1.csproj"", ""{A04C59CC-7622-4223-B16B-CDF2ECAD438D}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj2"", ""proj2\proj2.csproj"", ""{42641DAE-D6C4-49D4-92EA-749D2573554A}""
EndProject");

                Util.CreateFile(proj1Directory, "proj1.csproj",
                    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");
                Util.CreateFile(proj1Directory, "packages.config",
@"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
</packages>");

                Util.CreateFile(proj2Directory, "proj2.csproj",
                    @"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");
                Util.CreateFile(proj2Directory, "packages.config",
@"<packages>
  <package id=""packageB"" version=""2.2.0"" targetFramework=""net45"" />
</packages>");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    "restore -Source " + repositoryPath + " -PackageSaveMode nuspec",
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                var packageFileA = Path.Combine(workingPath, @"packages\packageA.1.1.0\packageA.1.1.0.nupkg");
                var nuspecFileA = Path.Combine(workingPath, @"packages\packageA.1.1.0\packageA.1.1.0.nuspec");
                var packageFileB = Path.Combine(workingPath, @"packages\packageB.2.2.0\packageB.2.2.0.nupkg");
                var nuspecFileB = Path.Combine(workingPath, @"packages\packageB.2.2.0\packageB.2.2.0.nuspec");
                Assert.True(!File.Exists(packageFileA));
                Assert.True(!File.Exists(packageFileB));
                Assert.True(File.Exists(nuspecFileA));
                Assert.True(File.Exists(nuspecFileB));
            }
        }

        // Tests restore from an http source.
        [Fact]
        public void RestoreCommand_FromHttpSource()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var package = new ZipPackage(packageFileName);

                Util.CreateFile(
                    workingDirectory,
                    "packages.config",
                    @"
<packages>
  <package id=""testPackage1"" version=""1.1.0"" />
</packages>");

                using (var server = new MockServer())
                {
                    bool getPackageByVersionIsCalled = false;
                    bool packageDownloadIsCalled = false;

                    server.Get.Add("/nuget/$metadata", r =>
                       MockServerResource.NuGetV2APIMetadata);
                    server.Get.Add("/nuget/Packages(Id='testPackage1',Version='1.1.0')", r =>
                        new Action<HttpListenerResponse>(response =>
                        {
                            getPackageByVersionIsCalled = true;
                            response.ContentType = "application/atom+xml;type=entry;charset=utf-8";
                            var odata = server.ToOData(package);
                            MockServer.SetResponseContent(response, odata);
                        }));

                    server.Get.Add("/package/testPackage1", r =>
                        new Action<HttpListenerResponse>(response =>
                        {
                            packageDownloadIsCalled = true;
                            response.ContentType = "application/zip";
                            using (var stream = package.GetStream())
                            {
                                var content = stream.ReadAllBytes();
                                MockServer.SetResponseContent(response, content);
                            }
                        }));

                    server.Get.Add("/nuget", r => "OK");

                    server.Start();

                    Util.CreateNuGetConfig(workingDirectory, new List<string>() { } );

                    // Act
                    var args = "restore packages.config -PackagesDirectory . -Source " + server.Uri + "nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        workingDirectory,
                        args,
                        waitForExit: true);
                    server.Stop();

                    // Assert
                    Assert.Equal(0, r1.Item1);
                    Assert.True(getPackageByVersionIsCalled, "getPackageByVersionIsCalled");
                    Assert.True(packageDownloadIsCalled, "getPackageByVersionIsCalled");
                }
            }
        }

        [Fact]
        public void RestoreCommand_FromProjectJson_RelativeGlobalPackagesFolder()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var basePath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var workingPath = Path.Combine(basePath, "sub1", "sub2");

                Directory.CreateDirectory(workingPath);

                var repositoryPath = Path.Combine(workingPath, "Repository");

                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));

                Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
                Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);
                Util.CreateFile(workingPath, "project.json",
@"{
  'dependencies': {
    'packageA': '1.1.0',
    'packageB': '2.2.0'
  },
  'frameworks': {
                'netcore50': { }
            }
}");

                Util.CreateFile(Path.Combine(workingPath, ".nuget"), "nuget.config",
@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <config>
    <add key=""globalPackagesFolder"" value=""..\..\GlobalPackages2"" />
  </config>
</configuration>");

                string[] args = new string[] {
                    "restore",
                    "-Source",
                    repositoryPath,
                    "-solutionDir",
                    workingPath,
                    "project.json",
                    "-verbosity detailed"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
                var packageFileA = Path.Combine(
                    workingPath,
                    @"..\..\GlobalPackages2\packageA\1.1.0\packageA.1.1.0.nupkg");

                var packageFileB = Path.Combine(
                    workingPath,
                    @"..\..\GlobalPackages2\packageB\2.2.0\packageB.2.2.0.nupkg");

                Assert.True(File.Exists(packageFileA));
                Assert.True(File.Exists(packageFileB));
            }
        }

        [Fact]
        public void RestoreCommand_FromProjectJson_RelativeGlobalPackagesFolder_NoSolutionDirectory()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var basePath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var workingPath = Path.Combine(basePath, "sub1", "sub2");
                var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(Path.Combine(workingPath, ".nuget"));

                Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
                Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);
                Util.CreateFile(workingPath, "project.json",
@"{
  'dependencies': {
    'packageA': '1.1.0',
    'packageB': '2.2.0'
  },
  'frameworks': {
                'netcore50': { }
            }
}");

                Util.CreateFile(workingPath, "nuget.config",
@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <config>
    <add key=""globalPackagesFolder"" value=""..\..\GlobalPackages2"" />
  </config>
</configuration>");

                string[] args = new string[] {
                    "restore",
                    "-Source",
                    repositoryPath,
                    "project.json"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.NotEqual(0, r.Item1);
                var error = r.Item3;
                Assert.True(error.Contains(NuGetResources.RestoreCommandCannotDetermineGlobalPackagesFolder));
            }
        }

        [Fact]
        public void RestoreCommand_InvalidPackagesConfigFile()
        {
            using (var randomTestFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var nugetexe = Util.GetNuGetExePath();
                var packagesConfigPath = Path.Combine(randomTestFolder, "packages.config");
                var contents = @"blah <packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
  <package id=""packageB"" version=""2.2.0"" targetFramework=""net45"" />
</packages>";
                File.WriteAllText(packagesConfigPath, contents);

                var args = new string[]
                {
                    "restore",
                    packagesConfigPath,
                    "-PackagesDirectory",
                    randomTestFolder
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.NotEqual(0, r.Item1);
                var error = r.Item3;
                Assert.True(error.Contains("Error parsing packages.config file"));
            }
        }

        [Fact]
        public void RestoreCommand_InvalidSolutionFile()
        {
            using (var randomTestFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var nugetexe = Util.GetNuGetExePath();
                var solutionFile = Path.Combine(randomTestFolder, "A.sln");
                var contents = @"Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(asdfasdf""{ FAE04EC0 - 301F - 11D3 - BF4B - 00C04F79EFBC}
                "") = ""proj1"", ""proj1\proj1.csproj"", ""{ A04C59CC - 7622 - 4223 - B16B - CDF2ECAD438D}
                ""
EndProjectblah
Project(""{ FAE04EC0 - 301F - 11D3 - BF4B - 00C04F79EFBC}
                "") = ""proj2"", ""proj2\proj2.csproj"", ""{ 42641DAE - D6C4 - 49D4 - 92EA - 749D2573554A}
                ""
EndProject";
                File.WriteAllText(solutionFile, contents);

                var args = new string[]
                {
                    "restore",
                    solutionFile,
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    randomTestFolder,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.NotEqual(0, r.Item1);
                var error = r.Item3;
                Assert.True(error.Contains("Error parsing solution file"));
            }
        }

        // return code should be 1 when restore failed
        [Fact]
        public void RestoreCommand_FromPackagesConfigFileFailed()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var repositoryPath = Path.Combine(workingPath, "Repository");
                Directory.CreateDirectory(repositoryPath);

                Util.CreateFile(workingPath, "packages.config",
@"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
  <package id=""packageB"" version=""2.2.0"" targetFramework=""net45"" />
</packages>");

                string[] args = new string[] { "restore", "-PackagesDirectory", "outputDir", "-Source", repositoryPath, "-nocache" };

                // Act
                var path = Environment.GetEnvironmentVariable("PATH");
                Environment.SetEnvironmentVariable("PATH", null);
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);
                Environment.SetEnvironmentVariable("PATH", path);

                // Assert
                Assert.Equal(1, r.Item1);
                Assert.False(r.Item2.IndexOf("exception", StringComparison.OrdinalIgnoreCase) > -1);
                Assert.False(r.Item3.IndexOf("exception", StringComparison.OrdinalIgnoreCase) > -1);

                var firstIndex = r.Item2.IndexOf("Unable to find version '1.1.0' of package 'packageA'.",
                    StringComparison.OrdinalIgnoreCase);
                Assert.True(firstIndex > -1);
                var secondIndex = r.Item2.IndexOf(
                    "Unable to find version '1.1.0' of package 'packageA'.",
                    firstIndex + 1,
                    StringComparison.OrdinalIgnoreCase);
                Assert.True(secondIndex > -1);
            }
        }

        [Fact]
        public void RestoreCommand_NoFeedAvailable()
        {
            var nugetexe = Util.GetNuGetExePath();
            using (var randomTestFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Create an empty config file and pass it as -ConfigFile switch.
                // This imitates the scenario where there is a machine without a default nuget.config under %APPDATA%
                // In this case, nuget will not create default nuget.config for user.
                var config = string.Format(
    @"<?xml version='1.0' encoding='utf - 8'?>
<configuration/>
");
                var configFileName = Path.Combine(randomTestFolder, "nuget.config");
                File.WriteAllText(configFileName, config);

                var packagesConfigFileName = Path.Combine(randomTestFolder, "packages.config");
                File.WriteAllText(
                    packagesConfigFileName,
@"<packages>
  <package id=""Newtonsoft.Json"" version=""7.0.1"" targetFramework=""net45"" />
</packages>");

                string[] args
                    = new string[]
                    {
                        "restore",
                        "-PackagesDirectory",
                        ".",
                        "-ConfigFile",
                        configFileName
                    };

                // Act
                var path = Environment.GetEnvironmentVariable("PATH");
                Environment.SetEnvironmentVariable("PATH", null);
                var r = CommandRunner.Run(
                    nugetexe,
                    randomTestFolder,
                    string.Join(" ", args),
                    waitForExit: true);
                Environment.SetEnvironmentVariable("PATH", path);

                // Assert
                var expectedPath = Path.Combine(
                    randomTestFolder,
                    "Newtonsoft.Json.7.0.1",
                    "Newtonsoft.Json.7.0.1.nupkg");

                Assert.False(File.Exists(expectedPath));
            }
        }

        [Fact]
        public void RestoreCommand_LegacySolutionLevelPackages_SolutionDirectory()
        {
            using (var randomRepositoryPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomSolutionFolder = TestFileSystemUtility.CreateRandomTestFolder())

            {
                // Arrange
                var nugetexe = Util.GetNuGetExePath();
                Util.CreateTestPackage("packageA", "1.1.0", randomRepositoryPath);
                Util.CreateTestPackage("packageB", "2.2.0", randomRepositoryPath);

                var solutionFile = Path.Combine(randomSolutionFolder, "A.sln");

                var solutionFileContents
                    = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") " +
@"= ""proj"", ""proj\proj.csproj"", ""{A04C59CC-7622-4223-B16B-CDF2ECAD438D}""
EndProject";

                File.WriteAllText(solutionFile, solutionFileContents);

                var nugetFolderAtSolutionDirectory
                    = Path.Combine(randomSolutionFolder, NuGetConstants.NuGetSolutionSettingsFolder);
                Directory.CreateDirectory(nugetFolderAtSolutionDirectory);

                File.WriteAllText(
                    Path.Combine(nugetFolderAtSolutionDirectory, Constants.PackageReferenceFile),
@"<packages>
  <package id=""packageB"" version=""2.2.0"" targetFramework=""net45"" />
</packages>");

                var projectDirectory = Path.Combine(randomSolutionFolder, "proj");
                Directory.CreateDirectory(projectDirectory);

                File.WriteAllText(
                    Path.Combine(projectDirectory, "proj.csproj"),
@"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");

                File.WriteAllText(
                    Path.Combine(projectDirectory, "packages.config"),
@"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
</packages>");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    randomSolutionFolder,
                    "restore " + randomSolutionFolder + " -Source " + randomRepositoryPath,
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
                var packageFileA = Path.Combine(randomSolutionFolder, @"packages\packageA.1.1.0\packageA.1.1.0.nupkg");
                var packageFileB = Path.Combine(randomSolutionFolder, @"packages\packageB.2.2.0\packageB.2.2.0.nupkg");
                Assert.True(File.Exists(packageFileA));
                Assert.True(File.Exists(packageFileB));
            }
        }

        [Fact]
        public void RestoreCommand_LegacySolutionLevelPackages_SolutionFile()
        {
            using (var randomRepositoryPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomSolutionFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var nugetexe = Util.GetNuGetExePath();
                Util.CreateTestPackage("packageA", "1.1.0", randomRepositoryPath);
                Util.CreateTestPackage("packageB", "2.2.0", randomRepositoryPath);

                var solutionFile = Path.Combine(randomSolutionFolder, "A.sln");

                var solutionFileContents
                    = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") " +
@"= ""proj"", ""proj\proj.csproj"", ""{A04C59CC-7622-4223-B16B-CDF2ECAD438D}""
EndProject";

                File.WriteAllText(solutionFile, solutionFileContents);

                var nugetFolderAtSolutionDirectory
                    = Path.Combine(randomSolutionFolder, NuGetConstants.NuGetSolutionSettingsFolder);
                Directory.CreateDirectory(nugetFolderAtSolutionDirectory);

                File.WriteAllText(
                    Path.Combine(nugetFolderAtSolutionDirectory, Constants.PackageReferenceFile),
@"<packages>
  <package id=""packageB"" version=""2.2.0"" targetFramework=""net45"" />
</packages>");

                var projectDirectory = Path.Combine(randomSolutionFolder, "proj");
                Directory.CreateDirectory(projectDirectory);

                File.WriteAllText(
                    Path.Combine(projectDirectory, "proj.csproj"),
@"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");

                File.WriteAllText(
                    Path.Combine(projectDirectory, "packages.config"),
@"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
</packages>");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    randomSolutionFolder,
                    "restore " + solutionFile + " -Source " + randomRepositoryPath,
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
                var packageFileA = Path.Combine(randomSolutionFolder, @"packages\packageA.1.1.0\packageA.1.1.0.nupkg");
                var packageFileB = Path.Combine(randomSolutionFolder, @"packages\packageB.2.2.0\packageB.2.2.0.nupkg");
                Assert.True(File.Exists(packageFileA));
                Assert.True(File.Exists(packageFileB));
            }
        }

        [Fact]
        public void RestoreCommand_LegacySolutionLevelPackages_NoArgument()
        {
            using (var randomRepositoryPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomSolutionFolder = TestFileSystemUtility.CreateRandomTestFolder())

            {
                // Arrange
                var nugetexe = Util.GetNuGetExePath();
                Util.CreateTestPackage("packageA", "1.1.0", randomRepositoryPath);
                Util.CreateTestPackage("packageB", "2.2.0", randomRepositoryPath);

                var solutionFile = Path.Combine(randomSolutionFolder, "A.sln");

                var solutionFileContents
                    = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") " +
@"= ""proj"", ""proj\proj.csproj"", ""{A04C59CC-7622-4223-B16B-CDF2ECAD438D}""
EndProject";

                File.WriteAllText(solutionFile, solutionFileContents);

                var nugetFolderAtSolutionDirectory
                    = Path.Combine(randomSolutionFolder, NuGetConstants.NuGetSolutionSettingsFolder);
                Directory.CreateDirectory(nugetFolderAtSolutionDirectory);

                File.WriteAllText(
                    Path.Combine(nugetFolderAtSolutionDirectory, Constants.PackageReferenceFile),
@"<packages>
  <package id=""packageB"" version=""2.2.0"" targetFramework=""net45"" />
</packages>");

                var projectDirectory = Path.Combine(randomSolutionFolder, "proj");
                Directory.CreateDirectory(projectDirectory);

                File.WriteAllText(
                    Path.Combine(projectDirectory, "proj.csproj"),
@"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");

                File.WriteAllText(
                    Path.Combine(projectDirectory, "packages.config"),
@"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
</packages>");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    randomSolutionFolder,
                    "restore  -Source " + randomRepositoryPath,
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
                var packageFileA = Path.Combine(randomSolutionFolder, @"packages\packageA.1.1.0\packageA.1.1.0.nupkg");
                var packageFileB = Path.Combine(randomSolutionFolder, @"packages\packageB.2.2.0\packageB.2.2.0.nupkg");
                Assert.True(File.Exists(packageFileA));
                Assert.True(File.Exists(packageFileB));
            }
        }

        [Fact]
        public void RestoreCommand_LegacySolutionLevelPackages_DuplicatePackageIds()
        {
            using (var randomRepositoryPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomSolutionFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var nugetexe = Util.GetNuGetExePath();
                Util.CreateTestPackage("packageA", "1.0.0", randomRepositoryPath);
                Util.CreateTestPackage("packageA", "2.0.0", randomRepositoryPath);
                Util.CreateTestPackage("packageA", "3.0.0", randomRepositoryPath);
                Util.CreateTestPackage("packageB", "1.0.0", randomRepositoryPath);
                Util.CreateTestPackage("packageB", "2.0.0", randomRepositoryPath);
                Util.CreateTestPackage("packageB", "3.0.0", randomRepositoryPath);
                Util.CreateTestPackage("packageC", "1.0.0", randomRepositoryPath);

                var solutionFile = Path.Combine(randomSolutionFolder, "A.sln");

                var solutionFileContents
                    = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") " +
@"= ""proj"", ""proj\proj.csproj"", ""{A04C59CC-7622-4223-B16B-CDF2ECAD438D}""
EndProject";

                File.WriteAllText(solutionFile, solutionFileContents);

                var nugetFolderAtSolutionDirectory
                    = Path.Combine(randomSolutionFolder, NuGetConstants.NuGetSolutionSettingsFolder);
                Directory.CreateDirectory(nugetFolderAtSolutionDirectory);

                File.WriteAllText(
                    Path.Combine(nugetFolderAtSolutionDirectory, Constants.PackageReferenceFile),
@"<packages>
  <package id=""packageB"" version=""1.0.0"" targetFramework=""net45"" />
  <package id=""packageB"" version=""2.0.0"" targetFramework=""net45"" />
  <package id=""packageB"" version=""3.0.0"" targetFramework=""net45"" />
  <package id=""packageA"" version=""1.0.0"" targetFramework=""net45"" />
  <package id=""packageA"" version=""2.0.0"" targetFramework=""net45"" />
  <package id=""packageA"" version=""3.0.0"" targetFramework=""net45"" />
</packages>");

                var projectDirectory = Path.Combine(randomSolutionFolder, "proj");
                Directory.CreateDirectory(projectDirectory);

                File.WriteAllText(
                    Path.Combine(projectDirectory, "proj.csproj"),
@"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");

                File.WriteAllText(
                    Path.Combine(projectDirectory, "packages.config"),
@"<packages>
  <package id=""packageC"" version=""1.0.0"" targetFramework=""net45"" />
</packages>");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    randomSolutionFolder,
                    "restore  -Source " + randomRepositoryPath,
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                Assert.True(File.Exists(Path.Combine(randomSolutionFolder,
                    @"packages\packageA.1.0.0\packageA.1.0.0.nupkg")));

                Assert.True(File.Exists(Path.Combine(randomSolutionFolder,
                    @"packages\packageA.2.0.0\packageA.2.0.0.nupkg")));

                Assert.True(File.Exists(Path.Combine(randomSolutionFolder,
                    @"packages\packageA.3.0.0\packageA.3.0.0.nupkg")));

                Assert.True(File.Exists(Path.Combine(randomSolutionFolder,
                    @"packages\packageB.1.0.0\packageB.1.0.0.nupkg")));

                Assert.True(File.Exists(Path.Combine(randomSolutionFolder,
                    @"packages\packageB.2.0.0\packageB.2.0.0.nupkg")));

                Assert.True(File.Exists(Path.Combine(randomSolutionFolder,
                    @"packages\packageB.3.0.0\packageB.3.0.0.nupkg")));

                Assert.True(File.Exists(Path.Combine(randomSolutionFolder,
                    @"packages\packageC.1.0.0\packageC.1.0.0.nupkg")));
            }
        }

        [Fact]
        public void RestoreCommand_LegacySolutionLevelPackages_DuplicatePackageIdentities()
        {
            using (var randomRepositoryPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomSolutionFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var nugetexe = Util.GetNuGetExePath();
                Util.CreateTestPackage("packageA", "1.0.0", randomRepositoryPath);
                Util.CreateTestPackage("packageA", "3.0.0", randomRepositoryPath);
                Util.CreateTestPackage("packageC", "1.0.0", randomRepositoryPath);

                var solutionFile = Path.Combine(randomSolutionFolder, "A.sln");

                var solutionFileContents
                    = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") " +
@"= ""proj"", ""proj\proj.csproj"", ""{A04C59CC-7622-4223-B16B-CDF2ECAD438D}""
EndProject";

                File.WriteAllText(solutionFile, solutionFileContents);

                var nugetFolderAtSolutionDirectory
                    = Path.Combine(randomSolutionFolder, NuGetConstants.NuGetSolutionSettingsFolder);
                Directory.CreateDirectory(nugetFolderAtSolutionDirectory);

                File.WriteAllText(
                    Path.Combine(nugetFolderAtSolutionDirectory, Constants.PackageReferenceFile),
@"<packages>
  <package id=""packageA"" version=""1.0.0"" targetFramework=""net45"" />
  <package id=""packageA"" version=""1.0.0"" targetFramework=""net45"" />
  <package id=""packageA"" version=""1.0.0"" targetFramework=""net45"" />
  <package id=""packageA"" version=""3.0.0"" targetFramework=""net45"" />
  <package id=""packageA"" version=""3.0.0"" targetFramework=""net45"" />
</packages>");

                var projectDirectory = Path.Combine(randomSolutionFolder, "proj");
                Directory.CreateDirectory(projectDirectory);

                File.WriteAllText(
                    Path.Combine(projectDirectory, "proj.csproj"),
@"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='packages.config' />
  </ItemGroup>
</Project>");

                File.WriteAllText(
                    Path.Combine(projectDirectory, "packages.config"),
@"<packages>
  <package id=""packageC"" version=""1.0.0"" targetFramework=""net45"" />
</packages>");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    randomSolutionFolder,
                    "restore  -Source " + randomRepositoryPath,
                    waitForExit: true);

                // Assert
                Assert.False(0 == r.Item1, r.Item2 + " " + r.Item3);
                Assert.Contains("There are duplicate packages: packageA.1.0.0, packageA.3.0.0", r.Item3);
            }
        }

        [Fact]
        public async Task RestoreCommand_PreservesPackageFilesTimestamps()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var workingPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var repositoryPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var entryModifiedTime = new DateTimeOffset(1985, 11, 20, 12, 0, 0, TimeSpan.FromHours(-7.0)).DateTime;

                var packageFileFullPath = Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
                using (var zip = new ZipArchive(File.Open(packageFileFullPath, FileMode.Open, FileAccess.ReadWrite), ZipArchiveMode.Update))
                {
                    var entry = await zip.AddEntryAsync("lib/net45/A.dll", string.Empty);
                    entry.LastWriteTime = entryModifiedTime;
                }

                Util.CreateFile(workingPath, "packages.config",
@"<packages>
  <package id=""packageA"" version=""1.1.0"" targetFramework=""net45"" />
</packages>");

                string[] args = new string[] { "restore", "-PackagesDirectory", "outputDir", "-Source", repositoryPath };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);

                var dllPath = Path.Combine(workingPath, "outputDir", "packageA.1.1.0", "lib", "net45", "A.dll");
                var dllFileInfo = new FileInfo(dllPath);
                Assert.True(File.Exists(dllFileInfo.FullName));
                Assert.Equal(entryModifiedTime, dllFileInfo.LastWriteTime);
            }
        }
    }
}
