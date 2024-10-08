// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class PluginDiscovererTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Constructor_AcceptsAnyString(string rawPluginPaths)
        {
            using (new PluginDiscoverer(rawPluginPaths))
            {
            }
        }

        [Fact]
        public async Task DiscoverAsync_ThrowsIfCancelled()
        {
            using (var discoverer = new PluginDiscoverer(rawPluginPaths: ""))
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => discoverer.DiscoverAsync(new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task DiscoverAsync_DoesNotThrowIfNoValidFilePathsAndFallbackEmbeddedSignatureVerifier()
        {
            using (var discoverer = new PluginDiscoverer(rawPluginPaths: ";"))
            {
                var pluginFiles = await discoverer.DiscoverAsync(CancellationToken.None);

                Assert.Empty(pluginFiles);
            }
        }

        [Fact]
        public async Task DiscoverAsync_PerformsDiscoveryOnlyOnce()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var pluginPath = Path.Combine(testDirectory.Path, "a");

                File.WriteAllText(pluginPath, string.Empty);

                var responses = new Dictionary<string, bool>()
                {
                    { pluginPath, true }
                };

                using (var discoverer = new PluginDiscoverer(pluginPath))
                {
                    var results = (await discoverer.DiscoverAsync(CancellationToken.None)).ToArray();

                    foreach (var result in results)
                    {
                        var pluginState = result.PluginFile.State.Value;
                    }

                    Assert.Equal(1, results.Length);

                    results = (await discoverer.DiscoverAsync(CancellationToken.None)).ToArray();

                    Assert.Equal(1, results.Length);
                }
            }
        }

        [Fact]
        public async Task DiscoverAsync_HandlesAllPluginFileStates()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var pluginPaths = new[] { "a", "b", }
                    .Select(fileName => Path.Combine(testDirectory.Path, fileName))
                    .ToArray();

                File.WriteAllText(pluginPaths[1], string.Empty);

                string rawPluginPaths =
                    $"{pluginPaths[0]};{pluginPaths[1]};c";

                using (var discoverer = new PluginDiscoverer(rawPluginPaths))
                {
                    var results = (await discoverer.DiscoverAsync(CancellationToken.None)).ToArray();

                    Assert.Equal(3, results.Length);

                    Assert.Equal(pluginPaths[0], results[0].PluginFile.Path);
                    Assert.Equal(PluginFileState.NotFound, results[0].PluginFile.State.Value);
                    Assert.Equal($"A plugin was not found at path '{pluginPaths[0]}'.", results[0].Message);

                    Assert.Equal(pluginPaths[1], results[1].PluginFile.Path);
                    Assert.Equal(PluginFileState.Valid, results[1].PluginFile.State.Value);
                    Assert.Null(results[1].Message);

                    Assert.Equal("c", results[2].PluginFile.Path);
                    Assert.Equal(PluginFileState.InvalidFilePath, results[2].PluginFile.State.Value);
                    Assert.Equal($"The plugin file path 'c' is invalid.", results[2].Message);
                }
            }
        }

        [Theory]
        [InlineData("a")]
        [InlineData(@"\a")]
        [InlineData(@".\a")]
        [InlineData(@"..\a")]
        public async Task DiscoverAsync_DisallowsNonRootedFilePaths(string pluginPath)
        {
            var responses = new Dictionary<string, bool>() { { pluginPath, true } };

            using (var discoverer = new PluginDiscoverer(pluginPath))
            {
                var results = (await discoverer.DiscoverAsync(CancellationToken.None)).ToArray();

                Assert.Equal(1, results.Length);
                Assert.Equal(pluginPath, results[0].PluginFile.Path);
                Assert.Equal(PluginFileState.InvalidFilePath, results[0].PluginFile.State.Value);
            }
        }

        [Fact]
        public async Task DiscoverAsync_IsIdempotent()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var pluginPath = Path.Combine(testDirectory.Path, "a");

                File.WriteAllText(pluginPath, string.Empty);

                using (var discoverer = new PluginDiscoverer(pluginPath))
                {
                    var firstResult = await discoverer.DiscoverAsync(CancellationToken.None);
                    var firstState = firstResult.SingleOrDefault().PluginFile.State.Value;

                    var secondResult = await discoverer.DiscoverAsync(CancellationToken.None);
                    var secondState = secondResult.SingleOrDefault().PluginFile.State.Value;

                    Assert.Same(firstResult, secondResult);
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("nuget-plugin-myPlugin.exe")]
        [InlineData("nuget-plugin-myPlugin.bat")]
        public async Task DiscoverAsync_withValidDotNetToolsPluginWindows_FindsThePlugin(string fileName)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Arrange
                var pluginPath = Path.Combine(testDirectory.Path, "myPlugin");
                Directory.CreateDirectory(pluginPath);
                var myPlugin = Path.Combine(pluginPath, fileName);
                Mock<IEnvironmentVariableReader> environmentalVariableReader = new Mock<IEnvironmentVariableReader>();
                environmentalVariableReader.Setup(env => env.GetEnvironmentVariable(It.IsAny<string>())).Returns(pluginPath);

                File.WriteAllText(myPlugin, string.Empty);
                var verifierSpy = new Mock<EmbeddedSignatureVerifier>();
                verifierSpy.Setup(spy => spy.IsValid(It.IsAny<string>()))
                    .Returns(true);

                using (var discoverer = new PluginDiscoverer("", verifierSpy.Object, environmentalVariableReader.Object))
                {
                    // Act
                    var result = await discoverer.DiscoverAsync(CancellationToken.None);

                    // Assert
                    var discovered = false;

                    foreach (PluginDiscoveryResult discoveryResult in result)
                    {
                        if (myPlugin == discoveryResult.PluginFile.Path) discovered = true;
                    }

                    Assert.True(discovered);
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("nuget-plugin-myPlugin.exe")]
        [InlineData("nuget-plugin-myPlugin.bat")]
        public async Task DiscoverAsync_WithPluginPathSpecifiedInNuGetPluginPathsEnvVariableWindows_FindsThePlugin(string fileName)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Arrange
                var pluginPath = Path.Combine(testDirectory.Path, "myPlugin");
                Directory.CreateDirectory(pluginPath);
                var myPlugin = Path.Combine(pluginPath, fileName);
                Mock<IEnvironmentVariableReader> environmentalVariableReader = new Mock<IEnvironmentVariableReader>();
                environmentalVariableReader.Setup(env => env.GetEnvironmentVariable("NUGET_PLUGIN_PATHS")).Returns(pluginPath);
                environmentalVariableReader.Setup(env => env.GetEnvironmentVariable("PATHS")).Returns("");
                File.WriteAllText(myPlugin, string.Empty);
                var verifierSpy = new Mock<EmbeddedSignatureVerifier>();
                verifierSpy.Setup(spy => spy.IsValid(It.IsAny<string>()))
                    .Returns(true);

                using (var discoverer = new PluginDiscoverer("", verifierSpy.Object, environmentalVariableReader.Object))
                {
                    // Act
                    var result = await discoverer.DiscoverAsync(CancellationToken.None);

                    // Assert
                    var discovered = false;

                    foreach (PluginDiscoveryResult discoveryResult in result)
                    {
                        if (myPlugin == discoveryResult.PluginFile.Path) discovered = true;
                    }

                    Assert.True(discovered);
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("nugetplugin-myPlugin.exe")]
        [InlineData("nugetplugin-myPlugin.bat")]
        public async Task DiscoverAsync_withInValidDotNetToolsPluginNameWindows_DoesNotFindThePlugin(string fileName)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Arrange
                var pluginPath = Path.Combine(testDirectory.Path, "myPlugin");
                Directory.CreateDirectory(pluginPath);
                var myPlugin = Path.Combine(pluginPath, fileName);
                Mock<IEnvironmentVariableReader> environmentalVariableReader = new Mock<IEnvironmentVariableReader>();
                environmentalVariableReader.Setup(env => env.GetEnvironmentVariable(It.IsAny<string>())).Returns(pluginPath);

                File.WriteAllText(myPlugin, string.Empty);
                var verifierSpy = new Mock<EmbeddedSignatureVerifier>();
                verifierSpy.Setup(spy => spy.IsValid(It.IsAny<string>()))
                    .Returns(true);

                using (var discoverer = new PluginDiscoverer("", verifierSpy.Object, environmentalVariableReader.Object))
                {
                    // Act
                    var result = await discoverer.DiscoverAsync(CancellationToken.None);

                    // Assert
                    var discovered = false;

                    foreach (PluginDiscoveryResult discoveryResult in result)
                    {
                        if (myPlugin == discoveryResult.PluginFile.Path) discovered = true;
                    }

                    Assert.False(discovered);
                }
            }
        }

        [PlatformFact(Platform.Linux)]
        public async Task DiscoverAsync_withValidDotNetToolsPluginLinux_FindsThePlugin()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Arrange
                var pluginPath = Path.Combine(testDirectory.Path, "myPlugins");
                Directory.CreateDirectory(pluginPath);
                var myPlugin = Path.Combine(pluginPath, "nuget-plugin-MyPlugin");
                File.WriteAllText(myPlugin, string.Empty);
                Mock<IEnvironmentVariableReader> environmentalVariableReader = new Mock<IEnvironmentVariableReader>();
                environmentalVariableReader.Setup(env => env.GetEnvironmentVariable(It.IsAny<string>())).Returns(pluginPath);

                using (var process = new Process())
                {
                    // Use a shell command to make the file executable
                    process.StartInfo.FileName = "/bin/bash";
                    process.StartInfo.Arguments = $"chmod +x {myPlugin}";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.Start();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        var verifierSpy = new Mock<EmbeddedSignatureVerifier>();
                        verifierSpy.Setup(spy => spy.IsValid(It.IsAny<string>()))
                            .Returns(true);

                        using (var discoverer = new PluginDiscoverer("", verifierSpy.Object, environmentalVariableReader.Object))
                        {
                            // Act
                            var result = await discoverer.DiscoverAsync(CancellationToken.None);

                            // Assert
                            var discovered = false;

                            foreach (PluginDiscoveryResult discoveryResult in result)
                            {
                                if (myPlugin == discoveryResult.PluginFile.Path) discovered = true;
                            }

                            Assert.True(discovered);
                        }
                    }
                }
            }
        }

        [PlatformFact(Platform.Linux)]
        public async Task DiscoverAsync_WithPluginPathSpecifiedInNuGetPluginPathsEnvVariableLinux_FindsThePlugin()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Arrange
                var pluginPath = Path.Combine(testDirectory.Path, "myPlugins");
                Directory.CreateDirectory(pluginPath);
                var myPlugin = Path.Combine(pluginPath, "nuget-plugin-MyPlugin");
                File.WriteAllText(myPlugin, string.Empty);
                Mock<IEnvironmentVariableReader> environmentalVariableReader = new Mock<IEnvironmentVariableReader>();
                environmentalVariableReader.Setup(env => env.GetEnvironmentVariable("NUGET_PLUGIN_PATHS")).Returns(pluginPath);
                environmentalVariableReader.Setup(env => env.GetEnvironmentVariable("PATHS")).Returns("");

                using (var process = new Process())
                {
                    // Use a shell command to make the file executable
                    process.StartInfo.FileName = "/bin/bash";
                    process.StartInfo.Arguments = $"chmod +x {myPlugin}";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.Start();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        var verifierSpy = new Mock<EmbeddedSignatureVerifier>();
                        verifierSpy.Setup(spy => spy.IsValid(It.IsAny<string>()))
                            .Returns(true);

                        using (var discoverer = new PluginDiscoverer("", verifierSpy.Object, environmentalVariableReader.Object))
                        {
                            // Act
                            var result = await discoverer.DiscoverAsync(CancellationToken.None);

                            // Assert
                            var discovered = false;

                            foreach (PluginDiscoveryResult discoveryResult in result)
                            {
                                if (myPlugin == discoveryResult.PluginFile.Path) discovered = true;
                            }

                            Assert.True(discovered);
                        }
                    }
                }
            }
        }

        [PlatformFact(Platform.Linux)]
        public async Task DiscoverAsync_withNoExecutableValidDotNetToolsPluginLinux_DoesNotFindThePlugin()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Arrange
                var pluginPath = Path.Combine(testDirectory.Path, "myPlugins");
                Directory.CreateDirectory(pluginPath);
                var myPlugin = Path.Combine(pluginPath, "nuget-plugin-MyPlugin");
                File.WriteAllText(myPlugin, string.Empty);
                Mock<IEnvironmentVariableReader> environmentalVariableReader = new Mock<IEnvironmentVariableReader>();
                environmentalVariableReader.Setup(env => env.GetEnvironmentVariable(It.IsAny<string>())).Returns(pluginPath);

                using (var process = new Process())
                {
                    // Use a shell command to make the file not executable
                    process.StartInfo.FileName = "/bin/bash";
                    process.StartInfo.Arguments = $"chmod -x {myPlugin}";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.Start();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        var verifierSpy = new Mock<EmbeddedSignatureVerifier>();
                        verifierSpy.Setup(spy => spy.IsValid(It.IsAny<string>()))
                            .Returns(true);

                        using (var discoverer = new PluginDiscoverer("", verifierSpy.Object, environmentalVariableReader.Object))
                        {
                            // Act
                            var result = await discoverer.DiscoverAsync(CancellationToken.None);

                            // Assert
                            var discovered = false;

                            foreach (PluginDiscoveryResult discoveryResult in result)
                            {
                                if (myPlugin == discoveryResult.PluginFile.Path) discovered = true;
                            }

                            Assert.False(discovered);
                        }
                    }
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public void GetPluginsInNuGetPluginPathsAndPath_WithNuGetPluginPaths_ReturnsPluginsInNuGetPluginPathOnly()
        {
            // Arrange
            TestDirectory pluginPathDirectory = TestDirectory.Create();
            TestDirectory pathDirectory = TestDirectory.Create();
            var pluginInNuGetPluginPathDirectoryFilePath = Path.Combine(pluginPathDirectory.Path, "nuget-plugin-auth.exe");
            var pluginInPathDirectoryFilePath = Path.Combine(pathDirectory.Path, "nuget-plugin-in-path-directory.exe");
            File.Create(pluginInNuGetPluginPathDirectoryFilePath);
            File.Create(pluginInPathDirectoryFilePath);
            Mock<IEnvironmentVariableReader> environmentalVariableReader = new Mock<IEnvironmentVariableReader>();
            environmentalVariableReader.Setup(env => env.GetEnvironmentVariable(EnvironmentVariableConstants.PluginPaths)).Returns(Directory.GetParent(pluginInNuGetPluginPathDirectoryFilePath).FullName);
            environmentalVariableReader.Setup(env => env.GetEnvironmentVariable("PATH")).Returns(Directory.GetParent(pluginInPathDirectoryFilePath).FullName);
            PluginDiscoverer pluginDiscoverer = new PluginDiscoverer("", Mock.Of<EmbeddedSignatureVerifier>(), environmentalVariableReader.Object);

            // Act
            var plugins = pluginDiscoverer.GetPluginsInNuGetPluginPathsAndPath();

            // Assert
            Assert.Single(plugins);
            Assert.Equal(pluginInNuGetPluginPathDirectoryFilePath, plugins[0].Path);
        }

        [PlatformFact(Platform.Windows)]
        public void GetPluginsInNuGetPluginPathsAndPath_WithoutNuGetPluginPaths_FallsBackToPath()
        {
            // Arrange
            var pathDirectory = TestDirectory.Create();
            var pluginFilePath = Path.Combine(pathDirectory.Path, "nuget-plugin-fallback.exe");
            File.Create(pluginFilePath);

            var environmentalVariableReader = new Mock<IEnvironmentVariableReader>();
            environmentalVariableReader.Setup(env => env.GetEnvironmentVariable(EnvironmentVariableConstants.PluginPaths)).Returns(string.Empty);
            environmentalVariableReader.Setup(env => env.GetEnvironmentVariable("PATH")).Returns(pathDirectory.Path);

            var pluginDiscoverer = new PluginDiscoverer("", Mock.Of<EmbeddedSignatureVerifier>(), environmentalVariableReader.Object);

            // Act
            var plugins = pluginDiscoverer.GetPluginsInNuGetPluginPathsAndPath();

            // Assert
            Assert.Single(plugins);
            Assert.Equal(pluginFilePath, plugins[0].Path);
        }

        [PlatformFact(Platform.Windows)]
        public void GetPluginsInNuGetPluginPathsAndPath_NuGetPluginPathsPointsToAFile_TreatsAsPlugin()
        {
            // Arrange
            var pluginFilePath = Path.Combine(TestDirectory.Create().Path, "nuget-plugin-auth.exe");
            File.Create(pluginFilePath);

            var environmentalVariableReader = new Mock<IEnvironmentVariableReader>();
            environmentalVariableReader.Setup(env => env.GetEnvironmentVariable(EnvironmentVariableConstants.PluginPaths)).Returns(pluginFilePath);
            environmentalVariableReader.Setup(env => env.GetEnvironmentVariable("PATH")).Returns(string.Empty);

            var pluginDiscoverer = new PluginDiscoverer("", Mock.Of<EmbeddedSignatureVerifier>(), environmentalVariableReader.Object);

            // Act
            var plugins = pluginDiscoverer.GetPluginsInNuGetPluginPathsAndPath();

            // Assert
            Assert.Single(plugins);
            Assert.Equal(pluginFilePath, plugins[0].Path);
        }

        [PlatformFact(Platform.Windows)]
        public void GetPluginsInNuGetPluginPathsAndPath_NuGetPluginPathsPointsToAFileThatDoesNotStartWithNugetPlugin_ReturnsNonDotnetPlugin()
        {
            // Arrange
            var pluginFilePath = Path.Combine(TestDirectory.Create().Path, "other-plugin.exe");
            File.Create(pluginFilePath);

            var environmentalVariableReader = new Mock<IEnvironmentVariableReader>();
            environmentalVariableReader.Setup(env => env.GetEnvironmentVariable(EnvironmentVariableConstants.PluginPaths)).Returns(pluginFilePath);
            environmentalVariableReader.Setup(env => env.GetEnvironmentVariable("PATH")).Returns(string.Empty);

            var pluginDiscoverer = new PluginDiscoverer("", Mock.Of<EmbeddedSignatureVerifier>(), environmentalVariableReader.Object);

            // Act
            var plugins = pluginDiscoverer.GetPluginsInNuGetPluginPathsAndPath();

            // Assert
            Assert.Single(plugins);
            Assert.False(plugins[0].IsDotnetToolsPlugin);
        }

        [PlatformFact(Platform.Windows)]
        public void GetPluginsInNuGetPluginPathsAndPath_NuGetPluginPathsPointsToADirectory_ContainsValidPluginFiles()
        {
            // Arrange
            var pluginPathDirectory = TestDirectory.Create();
            var validPluginFile = Path.Combine(pluginPathDirectory.Path, "nuget-plugin-auth.exe");
            var invalidPluginFile = Path.Combine(pluginPathDirectory.Path, "not-a-nuget-plugin.exe");
            File.Create(validPluginFile);
            File.Create(invalidPluginFile);

            var environmentalVariableReader = new Mock<IEnvironmentVariableReader>();
            environmentalVariableReader.Setup(env => env.GetEnvironmentVariable(EnvironmentVariableConstants.PluginPaths)).Returns(pluginPathDirectory.Path);
            environmentalVariableReader.Setup(env => env.GetEnvironmentVariable("PATH")).Returns(string.Empty);

            var pluginDiscoverer = new PluginDiscoverer("", Mock.Of<EmbeddedSignatureVerifier>(), environmentalVariableReader.Object);

            // Act
            var plugins = pluginDiscoverer.GetPluginsInNuGetPluginPathsAndPath();

            // Assert
            Assert.Single(plugins);
            Assert.Equal(validPluginFile, plugins[0].Path);
        }

        [PlatformFact(Platform.Windows)]
        public void GetPluginsInNuGetPluginPathsAndPath_NoEnvironmentVariables_ReturnsNoPlugins()
        {
            // Arrange
            var environmentalVariableReader = new Mock<IEnvironmentVariableReader>();
            environmentalVariableReader.Setup(env => env.GetEnvironmentVariable(EnvironmentVariableConstants.PluginPaths)).Returns(string.Empty);
            environmentalVariableReader.Setup(env => env.GetEnvironmentVariable("PATH")).Returns(string.Empty);

            var pluginDiscoverer = new PluginDiscoverer("", Mock.Of<EmbeddedSignatureVerifier>(), environmentalVariableReader.Object);

            // Act
            var plugins = pluginDiscoverer.GetPluginsInNuGetPluginPathsAndPath();

            // Assert
            Assert.Empty(plugins);
        }

        [PlatformFact(Platform.Windows)]
        public void GetPluginsInNuGetPluginPathsAndPath_PathContainsDirectoriesWithValidAndInvalidPlugins_ReturnsOnlyValidPlugins()
        {
            // Arrange
            var pathDirectory = TestDirectory.Create();
            var validPluginFile = Path.Combine(pathDirectory.Path, "nuget-plugin-valid.exe");
            var invalidPluginFile = Path.Combine(pathDirectory.Path, "random-file.exe");
            File.Create(validPluginFile);
            File.Create(invalidPluginFile);

            var environmentalVariableReader = new Mock<IEnvironmentVariableReader>();
            environmentalVariableReader.Setup(env => env.GetEnvironmentVariable(EnvironmentVariableConstants.PluginPaths)).Returns(string.Empty);
            environmentalVariableReader.Setup(env => env.GetEnvironmentVariable("PATH")).Returns(pathDirectory.Path);

            var pluginDiscoverer = new PluginDiscoverer("", Mock.Of<EmbeddedSignatureVerifier>(), environmentalVariableReader.Object);

            // Act
            var plugins = pluginDiscoverer.GetPluginsInNuGetPluginPathsAndPath();

            // Assert
            Assert.Single(plugins);
            Assert.Equal(validPluginFile, plugins[0].Path);
        }

        [PlatformFact(Platform.Windows)]
        public void GetPluginsInNuGetPluginPathsAndPath_NoNuGetPluginPaths_UsesPathEnvironment()
        {
            // Arrange
            TestDirectory testDirectory = TestDirectory.Create();
            var workingPath = testDirectory.Path;
            var pluginFilePath = Path.Combine(workingPath, "nuget-plugin-auth.exe");
            File.Create(pluginFilePath);
            Mock<IEnvironmentVariableReader> environmentalVariableReader = new Mock<IEnvironmentVariableReader>();
            environmentalVariableReader.Setup(env => env.GetEnvironmentVariable("PATH")).Returns(Directory.GetParent(pluginFilePath).FullName);
            PluginDiscoverer pluginDiscoverer = new PluginDiscoverer("", Mock.Of<EmbeddedSignatureVerifier>(), environmentalVariableReader.Object);

            // Act
            var plugins = pluginDiscoverer.GetPluginsInNuGetPluginPathsAndPath();

            // Assert
            Assert.Single(plugins);
            Assert.Equal(pluginFilePath, plugins[0].Path);
        }

        [Fact]
        public void GetPluginsInNuGetPluginPathsAndPath_NoPluginsFound_ReturnsEmptyList()
        {
            // Arrange
            TestDirectory testDirectory = TestDirectory.Create();
            var workingPath = testDirectory.Path;
            Mock<IEnvironmentVariableReader> environmentalVariableReader = new Mock<IEnvironmentVariableReader>();
            PluginDiscoverer pluginDiscoverer = new PluginDiscoverer("", Mock.Of<EmbeddedSignatureVerifier>(), environmentalVariableReader.Object);

            // Act
            var plugins = pluginDiscoverer.GetPluginsInNuGetPluginPathsAndPath();

            // Assert
            Assert.Empty(plugins);
        }

        [PlatformFact(Platform.Windows)]
        public void IsValidPluginFile_ExeFile_ReturnsTrue()
        {
            // Arrange
            TestDirectory testDirectory = TestDirectory.Create();
            var workingPath = testDirectory.Path;
            var pluginFilePath = Path.Combine(workingPath, "plugin.exe");
            File.Create(pluginFilePath);
            var fileInfo = new FileInfo(pluginFilePath);

            // Act
            bool result = PluginDiscoverer.IsValidPluginFile(fileInfo);

            // Assert
            Assert.True(result);
        }

        [PlatformFact(Platform.Windows)]
        public void IsValidPluginFile_Windows_NonExecutableFile_ReturnsFalse()
        {
            // Arrange
            TestDirectory testDirectory = TestDirectory.Create();
            var workingPath = testDirectory.Path;
            var nonPluginFilePath = Path.Combine(workingPath, "plugin.txt");
            File.Create(nonPluginFilePath);
            var fileInfo = new FileInfo(nonPluginFilePath);

            // Act
            bool result = PluginDiscoverer.IsValidPluginFile(fileInfo);

            // Assert
            Assert.False(result);
        }

        [PlatformFact(Platform.Linux)]
        public void IsValidPluginFile_Unix_ExecutableFile_ReturnsTrue()
        {
            // Arrange
            TestDirectory testDirectory = TestDirectory.Create();
            var workingPath = testDirectory.Path;
            var pluginFilePath = Path.Combine(workingPath, "plugin");
            File.Create(pluginFilePath).Dispose();

#if NET8_0_OR_GREATER
            // Set execute permissions
            File.SetUnixFileMode(pluginFilePath, UnixFileMode.UserExecute | UnixFileMode.UserRead);
#else
            // Use chmod to set execute permissions
            var process = new Process();
            process.StartInfo.FileName = "/bin/bash";
            process.StartInfo.Arguments = $"chmod +x {pluginFilePath}";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            process.WaitForExit();
#endif

            var fileInfo = new FileInfo(pluginFilePath);

            // Act
            bool result = PluginDiscoverer.IsValidPluginFile(fileInfo);

            // Assert
            Assert.True(result);
        }

#if !NET8_0_OR_GREATER
        [PlatformFact(Platform.Linux)]
        public void IsExecutable_FileIsExecutable_ReturnsTrue()
        {
            // Arrange
            TestDirectory testDirectory = TestDirectory.Create();
            var workingPath = testDirectory.Path;
            var pluginFilePath = Path.Combine(workingPath, "plugin");
            File.Create(pluginFilePath);

            // Set execute permissions
            var process = new Process();
            process.StartInfo.FileName = "/bin/bash";
            process.StartInfo.Arguments = $"chmod +x {pluginFilePath}";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            process.WaitForExit();

            var fileInfo = new FileInfo(pluginFilePath);

            // Act
            bool result = PluginDiscoverer.IsExecutable(fileInfo);

            // Assert
            Assert.True(result);
        }

        [PlatformFact(Platform.Linux)]
        public void IsExecutable_FileIsNotExecutable_ReturnsFalse()
        {
            // Arrange
            TestDirectory testDirectory = TestDirectory.Create();
            var workingPath = testDirectory.Path;
            var pluginFilePath = Path.Combine(workingPath, "plugin");
            File.Create(pluginFilePath);

            // Remove execute permissions
            var process = new Process();
            process.StartInfo.FileName = "/bin/bash";
            process.StartInfo.Arguments = $"chmod -x {pluginFilePath}";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            process.WaitForExit();

            var fileInfo = new FileInfo(pluginFilePath);

            // Act
            bool result = PluginDiscoverer.IsExecutable(fileInfo);

            // Assert
            Assert.False(result);
        }
#endif

        private sealed class EmbeddedSignatureVerifierStub : EmbeddedSignatureVerifier
        {
            private readonly Dictionary<string, bool> _responses;

            internal int IsValidCallCount { get; private set; }

            public EmbeddedSignatureVerifierStub(Dictionary<string, bool> responses)
            {
                _responses = responses;
            }

            public override bool IsValid(string filePath)
            {
                ++IsValidCallCount;

                bool value;

                Assert.True(_responses.TryGetValue(filePath, out value));

                return value;
            }
        }
    }
}
