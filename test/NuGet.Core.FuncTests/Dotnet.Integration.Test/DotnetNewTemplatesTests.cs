// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using FluentAssertions;
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
        [InlineData("", "", "", false)]
        [InlineData("ProjectA", "Myoutput", "VB", false)]
        [InlineData("", "", "C#", true)]
        [InlineData("ProjectA", "Myoutput", "F#", true)]
        public void Dotnet_New_ConsoleApp_Success(string projectName, string output, string lang, bool norestore)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                string effectiveOutput = output;
                var effectiveProjectName = new DirectoryInfo(pathContext.SolutionRoot).Name;

                if (string.IsNullOrEmpty(projectName))
                {
                    if (!string.IsNullOrEmpty(effectiveOutput))
                    {
                        effectiveProjectName = effectiveOutput;
                    }
                }
                else
                {
                    effectiveProjectName = projectName;

                    if (string.IsNullOrEmpty(effectiveOutput))
                    {
                        effectiveOutput = projectName;
                    }
                }

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string nameArg = string.IsNullOrEmpty(projectName) ? string.Empty : "-n " + projectName;
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
                            projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.csproj");
                            programFilePath = Path.Combine(projectDirectory, "Program.cs");
                            break;
                        case "VB":
                            projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.vbproj");
                            programFilePath = Path.Combine(projectDirectory, "Program.vb");
                            break;
                        case "F#":
                            projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.fsproj");
                            programFilePath = Path.Combine(projectDirectory, "Program.fs");
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.csproj");
                    programFilePath = Path.Combine(projectDirectory, "Program.cs");
                }

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new console {nameArg} {outputArg} {langArg} {norestoreArg}");

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
                }
            }
        }

        [Theory]
        [InlineData("", "", "", false)]
        [InlineData("ProjectA", "Myoutput", "VB", false)]
        [InlineData("", "", "C#", true)]
        [InlineData("ProjectA", "Myoutput", "F#", true)]
        public void Dotnet_New_Classlib_Success(string projectName, string output, string lang, bool norestore)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                string effectiveOutput = output;
                var effectiveProjectName = new DirectoryInfo(pathContext.SolutionRoot).Name;

                if (string.IsNullOrEmpty(projectName))
                {
                    if (!string.IsNullOrEmpty(effectiveOutput))
                    {
                        effectiveProjectName = effectiveOutput;
                    }
                }
                else
                {
                    effectiveProjectName = projectName;

                    if (string.IsNullOrEmpty(effectiveOutput))
                    {
                        effectiveOutput = projectName;
                    }
                }

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string nameArg = string.IsNullOrEmpty(projectName) ? string.Empty : "-n " + projectName;
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
                            projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.csproj");
                            libraryPath = Path.Combine(projectDirectory, "Class1.cs");
                            break;
                        case "VB":
                            projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.vbproj");
                            libraryPath = Path.Combine(projectDirectory, "Class1.vb");
                            break;
                        case "F#":
                            projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.fsproj");
                            libraryPath = Path.Combine(projectDirectory, "Library.fs");
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.csproj");
                    libraryPath = Path.Combine(projectDirectory, "Class1.cs");
                }

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new classlib {nameArg} {outputArg} {langArg} {norestoreArg}");

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
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_Wpf_NoRestore_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var effectiveOutput = "Myoutput";
                var effectiveProjectName = "ProjectA";

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string nameArg = "-n " + effectiveProjectName;
                string outputArg = "-o " + effectiveOutput;
                string norestoreArg = "--no-restore";
                var projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.csproj");
                var applicationXamlPath = Path.Combine(projectDirectory, "App.xaml.cs");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new wpf {nameArg} {outputArg} {norestoreArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(applicationXamlPath));
                Assert.False(Directory.Exists(Path.Combine(projectDirectory, "obj")));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_Wpf_Restore_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var effectiveOutput = "Myoutput";
                var effectiveProjectName = "ProjectA";

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string nameArg = "-n " + effectiveProjectName;
                string outputArg = "-o " + effectiveOutput;
                string langArg = "-lang VB";
                var projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.vbproj");
                var applicationXamlPath = Path.Combine(projectDirectory, "Application.xaml.vb");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new wpf {nameArg} {outputArg} {langArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(applicationXamlPath));
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_Wpflib_NoRestore_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var effectiveOutput = "Myoutput";
                var effectiveProjectName = "ProjectA";

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string nameArg = "-n " + effectiveProjectName;
                string outputArg = "-o " + effectiveOutput;
                string norestoreArg = "--no-restore";
                string projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.csproj");
                string libPath = Path.Combine(projectDirectory, "Class1.cs");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new wpflib {nameArg} {outputArg} {norestoreArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(libPath));
                Assert.False(Directory.Exists(Path.Combine(projectDirectory, "obj")));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_Wpflib_Restore_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var effectiveOutput = "Myoutput";
                var effectiveProjectName = "ProjectA";

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string nameArg = "-n " + effectiveProjectName;
                string outputArg = "-o " + effectiveOutput;
                string langArg = "-lang VB";
                string projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.vbproj");
                string libPath = Path.Combine(projectDirectory, "Class1.vb");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new wpflib {nameArg} {outputArg} {langArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(libPath));
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_WpfCustomControlLib_NoRestore_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var effectiveOutput = "Myoutput";
                var effectiveProjectName = "ProjectA";

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string nameArg = "-n " + effectiveProjectName;
                string outputArg = "-o " + effectiveOutput;
                string norestoreArg = "--no-restore";
                string projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.csproj");
                string customControlPath = Path.Combine(projectDirectory, "CustomControl1.cs");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new wpfcustomcontrollib {nameArg} {outputArg} {norestoreArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(customControlPath));
                Assert.False(Directory.Exists(Path.Combine(projectDirectory, "obj")));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_WpfCustomControlLib_Restore_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var effectiveOutput = "Myoutput";
                var effectiveProjectName = "ProjectA";

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string nameArg = "-n " + effectiveProjectName;
                string outputArg = "-o " + effectiveOutput;
                string langArg = "-lang VB";
                string projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.vbproj");
                string customControlPath = Path.Combine(projectDirectory, "CustomControl1.vb");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new wpfcustomcontrollib {nameArg} {outputArg} {langArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(customControlPath));
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_WpfUserControlLib_NoRestore_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var effectiveOutput = "Myoutput";
                var effectiveProjectName = "ProjectA";

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string nameArg = "-n " + effectiveProjectName;
                string outputArg = "-o " + effectiveOutput;
                string norestoreArg = "--no-restore";
                string projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.csproj");
                string userControlPath = Path.Combine(projectDirectory, "UserControl1.xaml.cs");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new wpfusercontrollib {nameArg} {outputArg} {norestoreArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(userControlPath));
                Assert.False(Directory.Exists(Path.Combine(projectDirectory, "obj")));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_WpfUserControlLib_Restore_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var effectiveOutput = "Myoutput";
                var effectiveProjectName = "ProjectA";

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string nameArg = "-n " + effectiveProjectName;
                string outputArg = "-o " + effectiveOutput;
                string langArg = "-lang VB";
                string projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.vbproj");
                string userControlPath = Path.Combine(projectDirectory, "UserControl1.xaml.vb");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new wpfusercontrollib {nameArg} {outputArg} {langArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(userControlPath));
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_Winforms_NoRestore_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var effectiveOutput = "Myoutput";
                var effectiveProjectName = "ProjectA";

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string nameArg = "-n " + effectiveProjectName;
                string outputArg = "-o " + effectiveOutput;
                string norestoreArg = "--no-restore";
                string projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.csproj");
                string formPath = Path.Combine(projectDirectory, "Form1.cs");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new winforms {nameArg} {outputArg} {norestoreArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(formPath));
                Assert.False(Directory.Exists(Path.Combine(projectDirectory, "obj")));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_Winforms_Restore_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var effectiveOutput = "Myoutput";
                var effectiveProjectName = "ProjectA";

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string nameArg = "-n " + effectiveProjectName;
                string outputArg = "-o " + effectiveOutput;
                string langArg = "-lang VB";
                string projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.vbproj");
                string formPath = Path.Combine(projectDirectory, "Form1.vb");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new winforms {nameArg} {outputArg} {langArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(formPath));
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_WinformsControlLib_NoRestore_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var effectiveOutput = "Myoutput";
                var effectiveProjectName = "ProjectA";

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string nameArg = "-n " + effectiveProjectName;
                string outputArg = "-o " + effectiveOutput;
                string norestoreArg = "--no-restore";
                string projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.csproj");
                string userControlPath = Path.Combine(projectDirectory, "UserControl1.cs");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new winformscontrollib {nameArg} {outputArg} {norestoreArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(userControlPath));
                Assert.False(Directory.Exists(Path.Combine(projectDirectory, "obj")));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_WinformsControlLib_Restore_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var effectiveOutput = "Myoutput";
                var effectiveProjectName = "ProjectA";

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string nameArg = "-n " + effectiveProjectName;
                string outputArg = "-o " + effectiveOutput;
                string langArg = "-lang VB";
                string projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.vbproj");
                string userControlPath = Path.Combine(projectDirectory, "UserControl1.vb");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new winformscontrollib {nameArg} {outputArg} {langArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(userControlPath));
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_WinformsLib_NoRestore_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var effectiveOutput = "Myoutput";
                var effectiveProjectName = "ProjectA";

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string nameArg = "-n " + effectiveProjectName;
                string outputArg = "-o " + effectiveOutput;
                string norestoreArg = "--no-restore";
                string projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.csproj");
                string libPath = Path.Combine(projectDirectory, "Class1.cs");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new winformslib {nameArg} {outputArg} {norestoreArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(libPath));
                Assert.False(Directory.Exists(Path.Combine(projectDirectory, "obj")));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_WinformsLib_Restore_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var effectiveOutput = "Myoutput";
                var effectiveProjectName = "ProjectA";

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string nameArg = "-n " + effectiveProjectName;
                string outputArg = "-o " + effectiveOutput;
                string langArg = "-lang VB";
                string projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.vbproj");
                string libPath = Path.Combine(projectDirectory, "Class1.vb");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new winformslib {nameArg} {outputArg} {langArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(libPath));
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
            }
        }

        [Theory]
        [InlineData("", "", "", false)]
        [InlineData("", "", "C#", true)]
        [InlineData("ProjectA", "Myoutput", "F#", true)]
        public void Dotnet_New_Worker_Success(string projectName, string output, string lang, bool norestore)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                string effectiveOutput = output;
                var effectiveProjectName = new DirectoryInfo(pathContext.SolutionRoot).Name;

                if (string.IsNullOrEmpty(projectName))
                {
                    if (!string.IsNullOrEmpty(effectiveOutput))
                    {
                        effectiveProjectName = effectiveOutput;
                    }
                }
                else
                {
                    effectiveProjectName = projectName;

                    if (string.IsNullOrEmpty(effectiveOutput))
                    {
                        effectiveOutput = projectName;
                    }
                }

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string nameArg = string.IsNullOrEmpty(projectName) ? string.Empty : "-n " + projectName;
                string outputArg = string.IsNullOrEmpty(effectiveOutput) ? string.Empty : "-o " + effectiveOutput;
                string langArg = string.IsNullOrEmpty(lang) ? string.Empty : "-lang " + lang;
                string norestoreArg = norestore ? "--no-restore" : string.Empty;
                var projectFilePath = string.Empty;
                var workerPath = string.Empty;

                if (!string.IsNullOrEmpty(lang))
                {
                    switch (lang)
                    {
                        case "C#":
                            projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.csproj");
                            workerPath = Path.Combine(projectDirectory, "Worker.cs");
                            break;
                        case "F#":
                            projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.fsproj");
                            workerPath = Path.Combine(projectDirectory, "Worker.fs");
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.csproj");
                    workerPath = Path.Combine(projectDirectory, "Worker.cs");
                }

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new worker {nameArg} {outputArg} {langArg} {norestoreArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(workerPath));

                if (norestore)
                {
                    Assert.False(Directory.Exists(Path.Combine(projectDirectory, "obj")));
                }
                else
                {
                    Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
                }
            }
        }

        [Theory]
        [InlineData("", "", "", false)]
        [InlineData("ProjectA", "Myoutput", "VB", false)]
        [InlineData("", "", "C#", true)]
        [InlineData("ProjectA", "Myoutput", "F#", true)]
        public void Dotnet_New_MsTest_Success(string projectName, string output, string lang, bool norestore)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                string effectiveOutput = output;
                var effectiveProjectName = new DirectoryInfo(pathContext.SolutionRoot).Name;

                if (string.IsNullOrEmpty(projectName))
                {
                    if (!string.IsNullOrEmpty(effectiveOutput))
                    {
                        effectiveProjectName = effectiveOutput;
                    }
                }
                else
                {
                    effectiveProjectName = projectName;

                    if (string.IsNullOrEmpty(effectiveOutput))
                    {
                        effectiveOutput = projectName;
                    }
                }

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, string.IsNullOrEmpty(effectiveOutput) ? string.Empty : effectiveOutput);
                string nameArg = string.IsNullOrEmpty(projectName) ? string.Empty : "-n " + projectName;
                string outputArg = string.IsNullOrEmpty(effectiveOutput) ? string.Empty : "-o " + effectiveOutput;
                string langArg = string.IsNullOrEmpty(lang) ? string.Empty : "-lang " + lang;
                string norestoreArg = norestore ? "--no-restore" : string.Empty;
                var projectFilePath = string.Empty;
                var workerPath = string.Empty;

                if (!string.IsNullOrEmpty(lang))
                {
                    switch (lang)
                    {
                        case "C#":
                            projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.csproj");
                            workerPath = Path.Combine(projectDirectory, "UnitTest1.cs");
                            break;
                        case "VB":
                            projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.vbproj");
                            workerPath = Path.Combine(projectDirectory, "UnitTest1.vb");
                            break;
                        case "F#":
                            projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.fsproj");
                            workerPath = Path.Combine(projectDirectory, "Tests.fs");
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    projectFilePath = Path.Combine(projectDirectory, $"{effectiveProjectName}.csproj");
                    workerPath = Path.Combine(projectDirectory, "UnitTest1.cs");
                }

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new mstest {nameArg} {outputArg} {langArg} {norestoreArg}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(workerPath));

                if (norestore)
                {
                    Assert.False(Directory.Exists(Path.Combine(projectDirectory, "obj")));
                }
                else
                {
                    Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
                }
            }
        }
    }
}
