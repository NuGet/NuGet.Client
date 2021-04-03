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

        [Fact]
        public void Dotnet_New_ConsoleApp_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;
                var projectDirectory = pathContext.SolutionRoot;
                var projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                var programFilePath = Path.Combine(projectDirectory, "Program.cs");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new console");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(programFilePath));
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
            }
        }

        [Fact]
        public void Dotnet_New_Classlib_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;
                var projectDirectory = pathContext.SolutionRoot;
                var projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                var classLibFilePath = Path.Combine(projectDirectory, "Class1.cs");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new classlib");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(classLibFilePath));
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_Wpf_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;
                var projectDirectory = pathContext.SolutionRoot;
                var projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                var applicationXamlPath = Path.Combine(projectDirectory, "App.xaml.cs");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new wpf");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(applicationXamlPath));
                Assert.True(Directory.Exists(Path.Combine(projectDirectory, "obj")));
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_Wpflib_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;
                var projectDirectory = pathContext.SolutionRoot;
                string projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                string libPath = Path.Combine(projectDirectory, "Class1.cs");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new wpflib");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(libPath));
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_WpfCustomControlLib_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;
                var projectDirectory = pathContext.SolutionRoot;
                string projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                string customControlPath = Path.Combine(projectDirectory, "CustomControl1.cs");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new wpfcustomcontrollib");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(customControlPath));
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_WpfUserControlLib_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;
                var projectDirectory = pathContext.SolutionRoot;
                string projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                string userControlPath = Path.Combine(projectDirectory, "UserControl1.xaml.cs");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new wpfusercontrollib");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(userControlPath));
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_Winforms_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;
                var projectDirectory = pathContext.SolutionRoot;
                string projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                string userControlPath = Path.Combine(projectDirectory, "UserControl1.xaml.cs");
                string formPath = Path.Combine(projectDirectory, "Form1.cs");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new winforms");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(formPath));
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_WinformsControlLib_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;
                var projectDirectory = pathContext.SolutionRoot;
                string projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                string userControlPath = Path.Combine(projectDirectory, "UserControl1.cs");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new winformscontrollib");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(userControlPath));
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_WinformsLib_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;
                var projectDirectory = pathContext.SolutionRoot;
                string projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                string libPath = Path.Combine(projectDirectory, "Class1.cs");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new winformslib");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(libPath));
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
            }
        }

        [Fact]
        public void Dotnet_New_Worker_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;
                var projectDirectory = pathContext.SolutionRoot;
                var projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                var workerPath = Path.Combine(projectDirectory, "Worker.cs");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new worker");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(workerPath));
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
            }
        }

        [Fact]
        public void Dotnet_New_MsTest_Success()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;
                var projectDirectory = pathContext.SolutionRoot;
                var projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
                var unitTestPath = Path.Combine(projectDirectory, "UnitTest1.cs");

                // Act
                CommandRunnerResult result = _fixture.RunDotnet(pathContext.SolutionRoot, $"new mstest");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                Assert.True(File.Exists(projectFilePath));
                Assert.True(File.Exists(unitTestPath));
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
            }
        }
    }
}
