// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Xml.Linq;
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

        [PlatformFact(Platform.Linux)]
        public void Dotnet_New_ConsoleApp_Pack_Success()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;
                var projectDirectory = pathContext.SolutionRoot;
                var nupkgPath = Path.Combine(projectDirectory, "bin", "Debug", $"{projectName}.1.0.0.nupkg");

                // Act
                CommandRunnerResult newResult = _fixture.RunDotnet(pathContext.SolutionRoot, "new console");
                CommandRunnerResult packResult = _fixture.RunDotnet(pathContext.SolutionRoot, "pack");

                // Assert
                // Make sure restore action was success.
                newResult.Success.Should().BeTrue(because: newResult.AllOutput);
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
                // Make sure pack action was success.
                packResult.Success.Should().BeTrue(because: packResult.AllOutput);
                Assert.True(File.Exists(nupkgPath));
            }
        }

        [PlatformFact(Platform.Linux)]
        public void Dotnet_New_Classlib_Pack_Success()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;
                var projectDirectory = pathContext.SolutionRoot;
                var nupkgPath = Path.Combine(projectDirectory, "bin", "Debug", $"{projectName}.1.0.0.nupkg");

                // Act
                CommandRunnerResult newResult = _fixture.RunDotnet(pathContext.SolutionRoot, "new classlib");
                CommandRunnerResult packResult = _fixture.RunDotnet(pathContext.SolutionRoot, "pack");

                // Assert
                // Make sure restore action was success.
                newResult.Success.Should().BeTrue(because: newResult.AllOutput);
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
                // Make sure pack action was success.
                packResult.Success.Should().BeTrue(because: packResult.AllOutput);
                Assert.True(File.Exists(nupkgPath));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_Wpf_Pack_Success()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;
                var projectDirectory = pathContext.SolutionRoot;
                var nupkgPath = Path.Combine(projectDirectory, "bin", "Debug", $"{projectName}.1.0.0.nupkg");

                // Act
                CommandRunnerResult newResult = _fixture.RunDotnet(pathContext.SolutionRoot, "new wpf");
                CommandRunnerResult packResult = _fixture.RunDotnet(pathContext.SolutionRoot, "pack");

                // Assert
                // Make sure restore action was success.
                newResult.Success.Should().BeTrue(because: newResult.AllOutput);
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
                // Make sure pack action was success.
                packResult.Success.Should().BeTrue(because: packResult.AllOutput);
                Assert.True(File.Exists(nupkgPath));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_Wpflib_Pack_Success()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;
                var projectDirectory = pathContext.SolutionRoot;
                var nupkgPath = Path.Combine(projectDirectory, "bin", "Debug", $"{projectName}.1.0.0.nupkg");

                // Act
                CommandRunnerResult newResult = _fixture.RunDotnet(pathContext.SolutionRoot, "new wpflib");
                CommandRunnerResult packResult = _fixture.RunDotnet(pathContext.SolutionRoot, "pack");

                // Assert
                // Make sure restore action was success.
                newResult.Success.Should().BeTrue(because: newResult.AllOutput);
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
                // Make sure pack action was success.
                packResult.Success.Should().BeTrue(because: packResult.AllOutput);
                Assert.True(File.Exists(nupkgPath));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_WpfCustomControlLib_Pack_Success()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;
                var projectDirectory = pathContext.SolutionRoot;
                var nupkgPath = Path.Combine(projectDirectory, "bin", "Debug", $"{projectName}.1.0.0.nupkg");

                // Act
                CommandRunnerResult newResult = _fixture.RunDotnet(pathContext.SolutionRoot, "new wpfcustomcontrollib");
                CommandRunnerResult packResult = _fixture.RunDotnet(pathContext.SolutionRoot, "pack");

                // Assert
                // Make sure restore action was success.
                newResult.Success.Should().BeTrue(because: newResult.AllOutput);
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
                // Make sure pack action was success.
                packResult.Success.Should().BeTrue(because: packResult.AllOutput);
                Assert.True(File.Exists(nupkgPath));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_Wpf_UserControlLib_Pack_Success()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;
                var projectDirectory = pathContext.SolutionRoot;
                var nupkgPath = Path.Combine(projectDirectory, "bin", "Debug", $"{projectName}.1.0.0.nupkg");

                // Act
                CommandRunnerResult newResult = _fixture.RunDotnet(pathContext.SolutionRoot, "new wpfusercontrollib");
                CommandRunnerResult packResult = _fixture.RunDotnet(pathContext.SolutionRoot, "pack");

                // Assert
                // Make sure restore action was success.
                newResult.Success.Should().BeTrue(because: newResult.AllOutput);
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
                // Make sure pack action was success.
                packResult.Success.Should().BeTrue(because: packResult.AllOutput);
                Assert.True(File.Exists(nupkgPath));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_Winforms_Pack_Success()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;
                var projectDirectory = pathContext.SolutionRoot;
                var nupkgPath = Path.Combine(projectDirectory, "bin", "Debug", $"{projectName}.1.0.0.nupkg");

                // Act
                CommandRunnerResult newResult = _fixture.RunDotnet(pathContext.SolutionRoot, "new winforms");
                CommandRunnerResult packResult = _fixture.RunDotnet(pathContext.SolutionRoot, "pack");

                // Assert
                // Make sure restore action was success.
                newResult.Success.Should().BeTrue(because: newResult.AllOutput);
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
                // Make sure pack action was success.
                packResult.Success.Should().BeTrue(because: packResult.AllOutput);
                Assert.True(File.Exists(nupkgPath));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_WinformsControlLib_Pack_Success()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;
                var projectDirectory = pathContext.SolutionRoot;
                var nupkgPath = Path.Combine(projectDirectory, "bin", "Debug", $"{projectName}.1.0.0.nupkg");

                // Act
                CommandRunnerResult newResult = _fixture.RunDotnet(pathContext.SolutionRoot, "new winformscontrollib");
                CommandRunnerResult packResult = _fixture.RunDotnet(pathContext.SolutionRoot, "pack");

                // Assert
                // Make sure restore action was success.
                newResult.Success.Should().BeTrue(because: newResult.AllOutput);
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
                // Make sure pack action was success.
                packResult.Success.Should().BeTrue(because: packResult.AllOutput);
                Assert.True(File.Exists(nupkgPath));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Dotnet_New_WinformsLib_Pack_Success()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;
                var projectDirectory = pathContext.SolutionRoot;
                var nupkgPath = Path.Combine(projectDirectory, "bin", "Debug", $"{projectName}.1.0.0.nupkg");

                // Act
                CommandRunnerResult newResult = _fixture.RunDotnet(pathContext.SolutionRoot, "new winformslib");
                CommandRunnerResult packResult = _fixture.RunDotnet(pathContext.SolutionRoot, "pack");

                // Assert
                // Make sure restore action was success.
                newResult.Success.Should().BeTrue(because: newResult.AllOutput);
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
                // Make sure pack action was success.
                packResult.Success.Should().BeTrue(because: packResult.AllOutput);
                Assert.True(File.Exists(nupkgPath));
            }
        }

        [PlatformFact(Platform.Linux)]
        public void Dotnet_New_Worker_Success()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;
                var projectDirectory = pathContext.SolutionRoot;
                var nupkgPath = Path.Combine(projectDirectory, "bin", "Debug", $"{projectName}.1.0.0.nupkg");

                // Act
                CommandRunnerResult newResult = _fixture.RunDotnet(pathContext.SolutionRoot, "new worker");

                // Assert
                // Make sure restore action was success.
                newResult.Success.Should().BeTrue(because: newResult.AllOutput);
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
                // Pack doesn't work because `IsPackage` is set to false.
            }
        }

        [PlatformFact(Platform.Linux)]
        public void Dotnet_New_MsTest_Success()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var projectName = new DirectoryInfo(pathContext.SolutionRoot).Name;
                var projectDirectory = pathContext.SolutionRoot;
                var nupkgPath = Path.Combine(projectDirectory, "bin", "Debug", $"{projectName}.1.0.0.nupkg");

                // Act
                CommandRunnerResult newResult = _fixture.RunDotnet(pathContext.SolutionRoot, $"new mstest");

                // Assert
                // Make sure restore action was success.
                newResult.Success.Should().BeTrue(because: newResult.AllOutput);
                Assert.True(File.Exists(Path.Combine(projectDirectory, "obj", "project.assets.json")));
                // Pack doesn't work because `IsPackage` is set to false.
            }
        }
    }
}
