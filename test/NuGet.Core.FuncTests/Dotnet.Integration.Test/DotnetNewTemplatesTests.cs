// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using FluentAssertions;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
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
        [InlineData("", "ProjectA", "", "VB", true)]
        [InlineData("", "", "", "F#", false)]
        [InlineData("", "ProjectA", "Myoutput", "VB", false)]
        [InlineData("net5.0", "", "", "", false)]
        [InlineData("net5.0", "", "", "C#", false)]
        [InlineData("net5.0", "ProjectA", "", "C#", false)]
        [InlineData("net5.0", "ProjectA", "", "F#", false)]
        [InlineData("netcoreapp3.1", "", "", "VB", true)]
        [InlineData("netcoreapp2.1", "", "", "", false)]
        [InlineData("netcoreapp3.1", "", "Myoutput", "", true)]
        public void Dotnet_New_ConsoleApp_Success(string targetFramework, string name, string output, string lang, bool norestore)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                string effectiveOutput = output;
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;

                if (string.IsNullOrEmpty(name))
                {
                    if (!string.IsNullOrEmpty(effectiveOutput))
                    {
                        projectName = effectiveOutput;
                    }
                }
                else
                {
                    projectName = name;

                    if (string.IsNullOrEmpty(effectiveOutput))
                    {
                        effectiveOutput = name;
                    }
                }

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string targetFrameworkArg = string.IsNullOrEmpty(targetFramework) ? string.Empty : "-f " + targetFramework;
                string nameArg = string.IsNullOrEmpty(name) ? string.Empty : "-n " + name;
                string outputArg = string.IsNullOrEmpty(effectiveOutput) ? string.Empty : "-o " + effectiveOutput;
                string langArg = string.IsNullOrEmpty(lang) ? string.Empty : "-lang " + lang;
                string norestoreArg = norestore ? "--no-restore" : string.Empty;
                var projectFilePath = string.Empty;
                var programFilePath = string.Empty;

                if (!string.IsNullOrEmpty(lang))
                {
                    switch (lang)
                    {
                        case "C#":
                            projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                            programFilePath = Path.Combine(projectDirectory, "Program.cs");
                            break;
                        case "VB":
                            projectFilePath = Path.Combine(projectDirectory, $"{projectName}.vbproj");
                            programFilePath = Path.Combine(projectDirectory, "Program.vb");
                            break;
                        case "F#":
                            projectFilePath = Path.Combine(projectDirectory, $"{projectName}.fsproj");
                            programFilePath = Path.Combine(projectDirectory, "Program.fs");
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                    programFilePath = Path.Combine(projectDirectory, "Program.cs");
                }

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new console {targetFrameworkArg} {nameArg} {outputArg} {langArg} {norestoreArg}".Trim());

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(programFilePath));

                if (norestore)
                {
                    Assert.False(Directory.Exists(Path.Combine(projectDirectory, "obj")));
                }
                else
                {
                    Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));

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

        [Theory]
        [InlineData("", "", "", "", false)]
        [InlineData("", "ProjectA", "", "VB", true)]
        [InlineData("", "", "", "F#", false)]
        [InlineData("", "ProjectA", "Myoutput", "VB", false)]
        [InlineData("net5.0", "", "", "", false)]
        [InlineData("net5.0", "", "", "C#", false)]
        [InlineData("net5.0", "ProjectA", "", "C#", false)]
        [InlineData("net5.0", "ProjectA", "", "F#", false)]
        [InlineData("netcoreapp3.1", "", "", "VB", true)]
        [InlineData("netcoreapp2.1", "", "", "", false)]
        [InlineData("netcoreapp3.1", "", "Myoutput", "", true)]
        [InlineData("netstandard2.0", "", "Myoutput", "", false)]
        [InlineData("netstandard2.1", "ProjectA", "Myoutput", "F#", false)]
        public void Dotnet_New_Classlib_Success(string targetFramework, string name, string output, string lang, bool norestore)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                string effectiveOutput = output;
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;

                if (string.IsNullOrEmpty(name))
                {
                    if (!string.IsNullOrEmpty(effectiveOutput))
                    {
                        projectName = effectiveOutput;
                    }
                }
                else
                {
                    projectName = name;

                    if (string.IsNullOrEmpty(effectiveOutput))
                    {
                        effectiveOutput = name;
                    }
                }

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string targetFrameworkArg = string.IsNullOrEmpty(targetFramework) ? string.Empty : "-f " + targetFramework;
                string nameArg = string.IsNullOrEmpty(name) ? string.Empty : "-n " + name;
                string outputArg = string.IsNullOrEmpty(effectiveOutput) ? string.Empty : "-o " + effectiveOutput;
                string langArg = string.IsNullOrEmpty(lang) ? string.Empty : "-lang " + lang;
                string norestoreArg = norestore ? "--no-restore" : string.Empty;
                var projectFilePath = string.Empty;
                var libraryPath = string.Empty;

                if (!string.IsNullOrEmpty(lang))
                {
                    switch (lang)
                    {
                        case "C#":
                            projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                            libraryPath = Path.Combine(projectDirectory, "Class1.cs");
                            break;
                        case "VB":
                            projectFilePath = Path.Combine(projectDirectory, $"{projectName}.vbproj");
                            libraryPath = Path.Combine(projectDirectory, "Class1.vb");
                            break;
                        case "F#":
                            projectFilePath = Path.Combine(projectDirectory, $"{projectName}.fsproj");
                            libraryPath = Path.Combine(projectDirectory, "Library.fs");
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                    libraryPath = Path.Combine(projectDirectory, "Class1.cs");
                }

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new classlib {targetFrameworkArg} {nameArg} {outputArg} {langArg} {norestoreArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(libraryPath));

                if (norestore)
                {
                    Assert.False(Directory.Exists(Path.Combine(projectDirectory, "obj")));
                }
                else
                {
                    Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));

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

        [Theory]
        [InlineData("", "", "", "", false)]
        [InlineData("", "ProjectA", "", "VB", true)]
        [InlineData("", "ProjectA", "Myoutput", "VB", false)]
        [InlineData("net5.0", "", "", "", false)]
        [InlineData("net5.0", "", "", "C#", false)]
        [InlineData("net5.0", "ProjectA", "", "C#", false)]
        [InlineData("netcoreapp3.1", "", "", "VB", true)]
        [InlineData("netcoreapp3.0", "", "", "", false)]
        [InlineData("netcoreapp3.1", "", "Myoutput", "", true)]
        public void Dotnet_New_Wpf_Success(string targetFramework, string name, string output, string lang, bool norestore)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                string effectiveOutput = output;
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;

                if (string.IsNullOrEmpty(name))
                {
                    if (!string.IsNullOrEmpty(effectiveOutput))
                    {
                        projectName = effectiveOutput;
                    }
                }
                else
                {
                    projectName = name;

                    if (string.IsNullOrEmpty(effectiveOutput))
                    {
                        effectiveOutput = name;
                    }
                }

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string targetFrameworkArg = string.IsNullOrEmpty(targetFramework) ? string.Empty : "-f " + targetFramework;
                string nameArg = string.IsNullOrEmpty(name) ? string.Empty : "-n " + name;
                string outputArg = string.IsNullOrEmpty(effectiveOutput) ? string.Empty : "-o " + effectiveOutput;
                string langArg = string.IsNullOrEmpty(lang) ? string.Empty : "-lang " + lang;
                string norestoreArg = norestore ? "--no-restore" : string.Empty;
                var projectFilePath = string.Empty;
                var applicationXamlPath = string.Empty;

                if (!string.IsNullOrEmpty(lang))
                {
                    switch (lang)
                    {
                        case "C#":
                            projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                            applicationXamlPath = Path.Combine(projectDirectory, "App.xaml.cs");
                            break;
                        case "VB":
                            projectFilePath = Path.Combine(projectDirectory, $"{projectName}.vbproj");
                            applicationXamlPath = Path.Combine(projectDirectory, "Application.xaml.vb");
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                    applicationXamlPath = Path.Combine(projectDirectory, "App.xaml.cs");
                }

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new wpf {targetFrameworkArg} {nameArg} {outputArg} {langArg} {norestoreArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(applicationXamlPath));

                if (norestore)
                {
                    Assert.False(Directory.Exists(Path.Combine(projectDirectory, "obj")));
                }
                else
                {
                    Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));

                    if (!string.IsNullOrWhiteSpace(targetFramework))
                    {
                        var format = new LockFileFormat();
                        LockFile assetFile = format.Read(Path.Combine(projectDirectory, "obj", "project.assets.json"));
                        PackageSpec packageSpec = assetFile.PackageSpec;

                        if (targetFramework == "netcoreapp3.0")
                        {
                            Assert.True(packageSpec.TargetFrameworks.Single().TargetAlias == targetFramework);
                        }
                        else
                        {
                            Assert.True(packageSpec.TargetFrameworks.Single().TargetAlias == targetFramework + "-windows");
                        }
                    }
                }
            }
        }

        [Theory]
        [InlineData("", "", "", "", false)]
        [InlineData("", "ProjectA", "", "VB", true)]
        [InlineData("", "ProjectA", "Myoutput", "VB", false)]
        [InlineData("net5.0", "", "", "", false)]
        [InlineData("net5.0", "", "", "C#", false)]
        [InlineData("net5.0", "ProjectA", "", "C#", false)]
        [InlineData("netcoreapp3.1", "", "", "VB", true)]
        [InlineData("netcoreapp3.0", "", "", "", false)]
        [InlineData("netcoreapp3.1", "", "Myoutput", "", true)]
        public void Dotnet_New_Wpflib_Success(string targetFramework, string name, string output, string lang, bool norestore)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                string effectiveOutput = output;
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;

                if (string.IsNullOrEmpty(name))
                {
                    if (!string.IsNullOrEmpty(effectiveOutput))
                    {
                        projectName = effectiveOutput;
                    }
                }
                else
                {
                    projectName = name;

                    if (string.IsNullOrEmpty(effectiveOutput))
                    {
                        effectiveOutput = name;
                    }
                }

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string targetFrameworkArg = string.IsNullOrEmpty(targetFramework) ? string.Empty : "-f " + targetFramework;
                string nameArg = string.IsNullOrEmpty(name) ? string.Empty : "-n " + name;
                string outputArg = string.IsNullOrEmpty(effectiveOutput) ? string.Empty : "-o " + effectiveOutput;
                string langArg = string.IsNullOrEmpty(lang) ? string.Empty : "-lang " + lang;
                string norestoreArg = norestore ? "--no-restore" : string.Empty;
                var projectFilePath = string.Empty;
                var libPath = string.Empty;

                if (!string.IsNullOrEmpty(lang))
                {
                    switch (lang)
                    {
                        case "C#":
                            projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                            libPath = Path.Combine(projectDirectory, "Class1.cs");
                            break;
                        case "VB":
                            projectFilePath = Path.Combine(projectDirectory, $"{projectName}.vbproj");
                            libPath = Path.Combine(projectDirectory, "Class1.vb");
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                    libPath = Path.Combine(projectDirectory, "Class1.cs");
                }

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new wpflib {targetFrameworkArg} {nameArg} {outputArg} {langArg} {norestoreArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(libPath));

                if (norestore)
                {
                    Assert.False(Directory.Exists(Path.Combine(projectDirectory, "obj")));
                }
                else
                {
                    Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));

                    if (!string.IsNullOrWhiteSpace(targetFramework))
                    {
                        var format = new LockFileFormat();
                        LockFile assetFile = format.Read(Path.Combine(projectDirectory, "obj", "project.assets.json"));
                        PackageSpec packageSpec = assetFile.PackageSpec;

                        if (targetFramework == "netcoreapp3.0")
                        {
                            Assert.True(packageSpec.TargetFrameworks.Single().TargetAlias == targetFramework);
                        }
                        else
                        {
                            Assert.True(packageSpec.TargetFrameworks.Single().TargetAlias == targetFramework + "-windows");
                        }
                    }
                }
            }
        }

        [Theory]
        [InlineData("", "", "", "", false)]
        [InlineData("", "ProjectA", "", "VB", true)]
        [InlineData("", "ProjectA", "Myoutput", "VB", false)]
        [InlineData("net5.0", "", "", "", false)]
        [InlineData("net5.0", "", "", "C#", false)]
        [InlineData("net5.0", "ProjectA", "", "C#", false)]
        [InlineData("netcoreapp3.1", "", "", "VB", true)]
        [InlineData("netcoreapp3.0", "", "", "", false)]
        [InlineData("netcoreapp3.1", "", "Myoutput", "", true)]
        public void Dotnet_New_WpfCustomControlLib_Success(string targetFramework, string name, string output, string lang, bool norestore)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                string effectiveOutput = output;
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;

                if (string.IsNullOrEmpty(name))
                {
                    if (!string.IsNullOrEmpty(effectiveOutput))
                    {
                        projectName = effectiveOutput;
                    }
                }
                else
                {
                    projectName = name;

                    if (string.IsNullOrEmpty(effectiveOutput))
                    {
                        effectiveOutput = name;
                    }
                }

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string targetFrameworkArg = string.IsNullOrEmpty(targetFramework) ? string.Empty : "-f " + targetFramework;
                string nameArg = string.IsNullOrEmpty(name) ? string.Empty : "-n " + name;
                string outputArg = string.IsNullOrEmpty(effectiveOutput) ? string.Empty : "-o " + effectiveOutput;
                string langArg = string.IsNullOrEmpty(lang) ? string.Empty : "-lang " + lang;
                string norestoreArg = norestore ? "--no-restore" : string.Empty;
                var projectFilePath = string.Empty;
                var customControlPath = string.Empty;

                if (!string.IsNullOrEmpty(lang))
                {
                    switch (lang)
                    {
                        case "C#":
                            projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                            customControlPath = Path.Combine(projectDirectory, "CustomControl1.cs");
                            break;
                        case "VB":
                            projectFilePath = Path.Combine(projectDirectory, $"{projectName}.vbproj");
                            customControlPath = Path.Combine(projectDirectory, "CustomControl1.vb");
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                    customControlPath = Path.Combine(projectDirectory, "CustomControl1.cs");
                }

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new wpfcustomcontrollib {targetFrameworkArg} {nameArg} {outputArg} {langArg} {norestoreArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(customControlPath));

                if (norestore)
                {
                    Assert.False(Directory.Exists(Path.Combine(projectDirectory, "obj")));
                }
                else
                {
                    Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));

                    if (!string.IsNullOrWhiteSpace(targetFramework))
                    {
                        var format = new LockFileFormat();
                        LockFile assetFile = format.Read(Path.Combine(projectDirectory, "obj", "project.assets.json"));
                        PackageSpec packageSpec = assetFile.PackageSpec;

                        if (targetFramework == "netcoreapp3.0")
                        {
                            Assert.True(packageSpec.TargetFrameworks.Single().TargetAlias == targetFramework);
                        }
                        else
                        {
                            Assert.True(packageSpec.TargetFrameworks.Single().TargetAlias == targetFramework + "-windows");
                        }
                    }
                }
            }
        }

        [Theory]
        [InlineData("", "", "", "", false)]
        [InlineData("", "ProjectA", "", "VB", true)]
        [InlineData("", "ProjectA", "Myoutput", "VB", false)]
        [InlineData("net5.0", "", "", "", false)]
        [InlineData("net5.0", "", "", "C#", false)]
        [InlineData("net5.0", "ProjectA", "", "C#", false)]
        [InlineData("netcoreapp3.1", "", "", "VB", true)]
        [InlineData("netcoreapp3.0", "", "", "", false)]
        [InlineData("netcoreapp3.1", "", "Myoutput", "", true)]
        public void Dotnet_New_WpfUserControlLib_Success(string targetFramework, string name, string output, string lang, bool norestore)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                string effectiveOutput = output;
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;

                if (string.IsNullOrEmpty(name))
                {
                    if (!string.IsNullOrEmpty(effectiveOutput))
                    {
                        projectName = effectiveOutput;
                    }
                }
                else
                {
                    projectName = name;

                    if (string.IsNullOrEmpty(effectiveOutput))
                    {
                        effectiveOutput = name;
                    }
                }

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string targetFrameworkArg = string.IsNullOrEmpty(targetFramework) ? string.Empty : "-f " + targetFramework;
                string nameArg = string.IsNullOrEmpty(name) ? string.Empty : "-n " + name;
                string outputArg = string.IsNullOrEmpty(effectiveOutput) ? string.Empty : "-o " + effectiveOutput;
                string langArg = string.IsNullOrEmpty(lang) ? string.Empty : "-lang " + lang;
                string norestoreArg = norestore ? "--no-restore" : string.Empty;
                var projectFilePath = string.Empty;
                var userControlPath = string.Empty;

                if (!string.IsNullOrEmpty(lang))
                {
                    switch (lang)
                    {
                        case "C#":
                            projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                            userControlPath = Path.Combine(projectDirectory, "UserControl1.xaml.cs");
                            break;
                        case "VB":
                            projectFilePath = Path.Combine(projectDirectory, $"{projectName}.vbproj");
                            userControlPath = Path.Combine(projectDirectory, "UserControl1.xaml.vb");
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                    userControlPath = Path.Combine(projectDirectory, "UserControl1.xaml.cs");
                }

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new wpfusercontrollib {targetFrameworkArg} {nameArg} {outputArg} {langArg} {norestoreArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(userControlPath));

                if (norestore)
                {
                    Assert.False(Directory.Exists(Path.Combine(projectDirectory, "obj")));
                }
                else
                {
                    Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));

                    if (!string.IsNullOrWhiteSpace(targetFramework))
                    {
                        var format = new LockFileFormat();
                        LockFile assetFile = format.Read(Path.Combine(projectDirectory, "obj", "project.assets.json"));
                        PackageSpec packageSpec = assetFile.PackageSpec;

                        if (targetFramework == "netcoreapp3.0")
                        {
                            Assert.True(packageSpec.TargetFrameworks.Single().TargetAlias == targetFramework);
                        }
                        else
                        {
                            Assert.True(packageSpec.TargetFrameworks.Single().TargetAlias == targetFramework + "-windows");
                        }
                    }
                }
            }
        }

        [Theory]
        [InlineData("", "", "", "", false)]
        [InlineData("", "ProjectA", "", "VB", true)]
        [InlineData("", "ProjectA", "Myoutput", "VB", false)]
        [InlineData("net5.0", "", "", "", false)]
        [InlineData("net5.0", "", "", "C#", false)]
        [InlineData("net5.0", "ProjectA", "", "C#", false)]
        [InlineData("netcoreapp3.1", "", "", "VB", true)]
        [InlineData("netcoreapp3.1", "", "Myoutput", "", true)]
        public void Dotnet_New_Winforms_Success(string targetFramework, string name, string output, string lang, bool norestore)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                string effectiveOutput = output;
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;

                if (string.IsNullOrEmpty(name))
                {
                    if (!string.IsNullOrEmpty(effectiveOutput))
                    {
                        projectName = effectiveOutput;
                    }
                }
                else
                {
                    projectName = name;

                    if (string.IsNullOrEmpty(effectiveOutput))
                    {
                        effectiveOutput = name;
                    }
                }

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string targetFrameworkArg = string.IsNullOrEmpty(targetFramework) ? string.Empty : "-f " + targetFramework;
                string nameArg = string.IsNullOrEmpty(name) ? string.Empty : "-n " + name;
                string outputArg = string.IsNullOrEmpty(effectiveOutput) ? string.Empty : "-o " + effectiveOutput;
                string langArg = string.IsNullOrEmpty(lang) ? string.Empty : "-lang " + lang;
                string norestoreArg = norestore ? "--no-restore" : string.Empty;
                var projectFilePath = string.Empty;
                var formPath = string.Empty;

                if (!string.IsNullOrEmpty(lang))
                {
                    switch (lang)
                    {
                        case "C#":
                            projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                            formPath = Path.Combine(projectDirectory, "Form1.cs");
                            break;
                        case "VB":
                            projectFilePath = Path.Combine(projectDirectory, $"{projectName}.vbproj");
                            formPath = Path.Combine(projectDirectory, "Form1.vb");
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                    formPath = Path.Combine(projectDirectory, "Form1.cs");
                }

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new winforms {targetFrameworkArg} {nameArg} {outputArg} {langArg} {norestoreArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(formPath));

                if (norestore)
                {
                    Assert.False(Directory.Exists(Path.Combine(projectDirectory, "obj")));
                }
                else
                {
                    Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));

                    if (!string.IsNullOrWhiteSpace(targetFramework))
                    {
                        var format = new LockFileFormat();
                        LockFile assetFile = format.Read(Path.Combine(projectDirectory, "obj", "project.assets.json"));
                        PackageSpec packageSpec = assetFile.PackageSpec;
                        Assert.True(packageSpec.TargetFrameworks.Single().TargetAlias == targetFramework + "-windows");
                    }
                }
            }
        }

        [Theory]
        [InlineData("", "", "", "", false)]
        [InlineData("", "ProjectA", "", "VB", true)]
        [InlineData("", "ProjectA", "Myoutput", "VB", false)]
        [InlineData("net5.0", "", "", "", false)]
        [InlineData("net5.0", "", "", "C#", false)]
        [InlineData("net5.0", "ProjectA", "", "C#", false)]
        [InlineData("netcoreapp3.1", "", "", "VB", true)]
        [InlineData("netcoreapp3.1", "", "Myoutput", "", true)]
        public void Dotnet_New_WinformsControlLib_Success(string targetFramework, string name, string output, string lang, bool norestore)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                string effectiveOutput = output;
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;

                if (string.IsNullOrEmpty(name))
                {
                    if (!string.IsNullOrEmpty(effectiveOutput))
                    {
                        projectName = effectiveOutput;
                    }
                }
                else
                {
                    projectName = name;

                    if (string.IsNullOrEmpty(effectiveOutput))
                    {
                        effectiveOutput = name;
                    }
                }

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string targetFrameworkArg = string.IsNullOrEmpty(targetFramework) ? string.Empty : "-f " + targetFramework;
                string nameArg = string.IsNullOrEmpty(name) ? string.Empty : "-n " + name;
                string outputArg = string.IsNullOrEmpty(effectiveOutput) ? string.Empty : "-o " + effectiveOutput;
                string langArg = string.IsNullOrEmpty(lang) ? string.Empty : "-lang " + lang;
                string norestoreArg = norestore ? "--no-restore" : string.Empty;
                var projectFilePath = string.Empty;
                var userControlPath = string.Empty;

                if (!string.IsNullOrEmpty(lang))
                {
                    switch (lang)
                    {
                        case "C#":
                            projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                            userControlPath = Path.Combine(projectDirectory, "UserControl1.cs");
                            break;
                        case "VB":
                            projectFilePath = Path.Combine(projectDirectory, $"{projectName}.vbproj");
                            userControlPath = Path.Combine(projectDirectory, "UserControl1.vb");
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                    userControlPath = Path.Combine(projectDirectory, "UserControl1.cs");
                }

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new winformscontrollib {targetFrameworkArg} {nameArg} {outputArg} {langArg} {norestoreArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(userControlPath));

                if (norestore)
                {
                    Assert.False(Directory.Exists(Path.Combine(projectDirectory, "obj")));
                }
                else
                {
                    Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));

                    if (!string.IsNullOrWhiteSpace(targetFramework))
                    {
                        var format = new LockFileFormat();
                        LockFile assetFile = format.Read(Path.Combine(projectDirectory, "obj", "project.assets.json"));
                        PackageSpec packageSpec = assetFile.PackageSpec;
                        Assert.True(packageSpec.TargetFrameworks.Single().TargetAlias == targetFramework + "-windows");
                    }
                }
            }
        }

        [Theory]
        [InlineData("", "", "", "", false)]
        [InlineData("", "ProjectA", "", "VB", true)]
        [InlineData("", "ProjectA", "Myoutput", "VB", false)]
        [InlineData("net5.0", "", "", "", false)]
        [InlineData("net5.0", "", "", "C#", false)]
        [InlineData("net5.0", "ProjectA", "", "C#", false)]
        [InlineData("netcoreapp3.1", "", "", "VB", true)]
        [InlineData("netcoreapp3.1", "", "Myoutput", "", true)]
        public void Dotnet_New_WinformsLib_Success(string targetFramework, string name, string output, string lang, bool norestore)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                string effectiveOutput = output;
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;

                if (string.IsNullOrEmpty(name))
                {
                    if (!string.IsNullOrEmpty(effectiveOutput))
                    {
                        projectName = effectiveOutput;
                    }
                }
                else
                {
                    projectName = name;

                    if (string.IsNullOrEmpty(effectiveOutput))
                    {
                        effectiveOutput = name;
                    }
                }

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string targetFrameworkArg = string.IsNullOrEmpty(targetFramework) ? string.Empty : "-f " + targetFramework;
                string nameArg = string.IsNullOrEmpty(name) ? string.Empty : "-n " + name;
                string outputArg = string.IsNullOrEmpty(effectiveOutput) ? string.Empty : "-o " + effectiveOutput;
                string langArg = string.IsNullOrEmpty(lang) ? string.Empty : "-lang " + lang;
                string norestoreArg = norestore ? "--no-restore" : string.Empty;
                var projectFilePath = string.Empty;
                var libPath = string.Empty;

                if (!string.IsNullOrEmpty(lang))
                {
                    switch (lang)
                    {
                        case "C#":
                            projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                            libPath = Path.Combine(projectDirectory, "Class1.cs");
                            break;
                        case "VB":
                            projectFilePath = Path.Combine(projectDirectory, $"{projectName}.vbproj");
                            libPath = Path.Combine(projectDirectory, "Class1.vb");
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                    libPath = Path.Combine(projectDirectory, "Class1.cs");
                }

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new winformslib {targetFrameworkArg} {nameArg} {outputArg} {langArg} {norestoreArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(libPath));

                if (norestore)
                {
                    Assert.False(Directory.Exists(Path.Combine(projectDirectory, "obj")));
                }
                else
                {
                    Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));

                    if (!string.IsNullOrWhiteSpace(targetFramework))
                    {
                        var format = new LockFileFormat();
                        LockFile assetFile = format.Read(Path.Combine(projectDirectory, "obj", "project.assets.json"));
                        PackageSpec packageSpec = assetFile.PackageSpec;
                        Assert.True(packageSpec.TargetFrameworks.Single().TargetAlias == targetFramework + "-windows");
                    }
                }
            }
        }
    }
}
