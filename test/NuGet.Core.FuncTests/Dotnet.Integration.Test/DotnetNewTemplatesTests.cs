// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using FluentAssertions;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using NuGet.XPlat.FuncTest;
using Xunit;

namespace Dotnet.Integration.Test
{
    [Collection("Dotnet Integration Tests")]
    public class DotnetNewTemplatesTests
    {
        private readonly MsbuildIntegrationTestFixture _fixture;

        public DotnetNewTemplatesTests(MsbuildIntegrationTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Theory]
        [InlineData("", "", "", "", false)]
        [InlineData("", "ProjectA", "","VB", true)]
        [InlineData("", "ProjectA", "Myoutput", "VB", false)]
        [InlineData("net5.0", "", "", "", false)]
        [InlineData("net5.0", "", "", "C#", false)]
        [InlineData("net5.0", "ProjectA", "", "C#", false)]
        [InlineData("netcoreapp3.1", "", "", "VB", true)]
        [InlineData("netcoreapp2.1", "", "", "", false)]
        public void Dotnet_NewConsoleApp_Success(string targetFramework, string name, string output, string lang, bool norestore)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                output = string.IsNullOrEmpty(output) ? name : output;
                var projectName = string.IsNullOrEmpty(name) ? new DirectoryInfo(pathContext.SolutionRoot).Name : name;
                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(output) ? string.Empty : output);
                string targetFrameworkArg = string.IsNullOrEmpty(targetFramework) ? string.Empty : "-f "+ targetFramework;
                string nameArg = string.IsNullOrEmpty(name) ? string.Empty : "-n "+ name;
                string outputArg = string.IsNullOrEmpty(output) ? string.Empty : "-o "+ output;
                string langArg = string.IsNullOrEmpty(lang) ? string.Empty : "-lang " + lang;
                string norestoreArg = norestore ? "--no-restore" : string.Empty;
                var projectFilePath = string.Empty;

                if (!string.IsNullOrEmpty(lang))
                {
                    switch (lang)
                    {
                        case "C#":
                            projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                            break;
                        case "VB":
                            projectFilePath = Path.Combine(projectDirectory, $"{projectName}.vbproj");
                            break;
                        case "F#":
                            projectFilePath = Path.Combine(projectDirectory, $"{projectName}.fsproj");
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                }

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new console {targetFrameworkArg} {nameArg} {outputArg} {langArg} {norestoreArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                
                if(norestore)
                {
                    Assert.False(Directory.Exists(Path.Combine(projectDirectory, "obj")));
                }
                else
                {
                    Assert.True(Directory.Exists(Path.Combine(projectDirectory, "obj")));

                    if (!string.IsNullOrWhiteSpace(targetFramework))
                    {
                        var format = new LockFileFormat();
                        LockFile assetFile = format.Read(Path.Combine(projectDirectory, "obj", "project.assets.json"));
                        PackageSpec packageSpec = assetFile.PackageSpec;
                        Assert.True(packageSpec.TargetFrameworks.Single().TargetAlias == targetFramework);
                    }
                }
            }
        }
    }
}
