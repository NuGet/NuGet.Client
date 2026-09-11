// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System.IO;
using FluentAssertions;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit;
using Xunit.Abstractions;

namespace Msbuild.Integration.Test
{
    public class MsbuildPackTaskTests : IClassFixture<MsbuildIntegrationTestFixture>
    {
        private readonly MsbuildIntegrationTestFixture _msbuildFixture;
        private readonly ITestOutputHelper _testOutputHelper;

        public MsbuildPackTaskTests(MsbuildIntegrationTestFixture fixture, ITestOutputHelper testOutputHelper)
        {
            _msbuildFixture = fixture;
            _testOutputHelper = testOutputHelper;
        }

        [PlatformFact(Platform.Windows)]
        public void MsbuildPack_ClassicPackageReferenceProjectWithNonStandardPackageId_EmitsNU5052()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = "ClassLibrary1";
                var projectDirectory = Path.Combine(pathContext.SolutionRoot, projectName);
                var projectPath = Path.Combine(projectDirectory, $"{projectName}.csproj");

                Directory.CreateDirectory(projectDirectory);
                File.WriteAllText(Path.Combine(projectDirectory, "Class1.cs"), "public class Class1 { }");
                File.WriteAllText(projectPath,
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""14.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <Import Project=""$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"" Condition=""Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')"" />
  <PropertyGroup>
    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
    <OutputType>Library</OutputType>
    <TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>
    <AssemblyName>{projectName}</AssemblyName>
    <RootNamespace>{projectName}</RootNamespace>
    <RestoreProjectStyle>PackageReference</RestoreProjectStyle>
    <PackageId>Contöso.Utilities</PackageId>
    <PackageVersion>1.0.0</PackageVersion>
    <IncludeBuildOutput>false</IncludeBuildOutput>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include=""Class1.cs"" />
  </ItemGroup>
  <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
  <Import Project=""$(NuGetRestoreTargets)"" />
  <Import Project=""$(NuGetBuildTasksPackTargets)"" />
</Project>");

                var restoreResult = _msbuildFixture.RunMsBuild(
                    pathContext.WorkingDirectory,
                    $@"/t:restore ""{projectPath}""",
                    ignoreExitCode: true,
                    testOutputHelper: _testOutputHelper);
                restoreResult.ExitCode.Should().Be(0, restoreResult.AllOutput);

                var packResult = _msbuildFixture.RunMsBuild(
                    pathContext.WorkingDirectory,
                    $@"/t:pack ""{projectPath}"" /p:NoBuild=true /p:PackageOutputPath=""{pathContext.WorkingDirectory}""",
                    ignoreExitCode: true,
                    testOutputHelper: _testOutputHelper);

                packResult.ExitCode.Should().Be(0, packResult.AllOutput);
                packResult.AllOutput.Should().Contain("NU5052");
            }
        }
    }
}
