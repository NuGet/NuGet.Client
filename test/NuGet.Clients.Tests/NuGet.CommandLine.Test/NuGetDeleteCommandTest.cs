// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetDeleteCommandTest
    {
        private const string ApiKeyHeader = "X-NuGet-ApiKey";
        private static readonly string NuGetExePath = Util.GetNuGetExePath();
        private string _httpErrorSingle = "You are running the '{0}' operation with an 'HTTP' source: {1}. NuGet requires HTTPS sources. To use an HTTP source, you must explicitly set 'allowInsecureConnections' to true in your NuGet.Config file. Please refer to https://aka.ms/nuget-https-everywhere.";

        // Tests deleting a package from a source that is a file system directory.
        [Fact]
        public void DeleteCommand_DeleteFromV2FileSystemSource()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var source = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", source);
                Assert.True(File.Exists(packageFileName));

                // Act
                string[] args = new string[] {
                    "delete", "testPackage1", "1.1.0",
                    "-Source", source, "-NonInteractive" };
                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    String.Join(" ", args));

                // Assert
                Assert.Equal(0, r.ExitCode);
                Assert.False(File.Exists(packageFileName));
            }
        }

        [Fact]
        public void DeleteCommand_DeleteReadOnlyFromV2FileSystemSource()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var source = TestDirectory.Create())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", source);
                Assert.True(File.Exists(packageFileName));
                File.SetAttributes(packageFileName,
                    File.GetAttributes(packageFileName) | FileAttributes.ReadOnly);
                // Act
                string[] args = new string[] {
                    "delete", "testPackage1", "1.1.0",
                    "-Source", source, "-NonInteractive" };
                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    String.Join(" ", args));

                // Assert
                Assert.Equal(0, r.ExitCode);
                Assert.False(File.Exists(packageFileName));
            }
        }

        [Fact]
        public void DeleteCommand_DeleteFromV3FileSystemSource()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var source = TestDirectory.Create())
            {
                //drop dummy artifacts to make it a V3
                var dummyPackageName = "foo";
                var version = "1.0.0";
                var packageFolder = Directory.CreateDirectory(Path.Combine(source.Path, dummyPackageName));
                var packageVersionFolder = Directory.CreateDirectory(Path.Combine(packageFolder.FullName, "1.0.0"));
                File.WriteAllText(Path.Combine(packageVersionFolder.FullName, dummyPackageName + ".nuspec"), "dummy text");
                Assert.True(Directory.Exists(packageVersionFolder.FullName));
                // Act
                string[] args = new string[] {
                    "delete", "foo", version,
                    "-Source", source, "-NonInteractive" };
                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    String.Join(" ", args));

                // Assert
                Assert.Equal(0, r.ExitCode);
                //The specific version folder should be gone.
                Assert.False(Directory.Exists(packageVersionFolder.FullName));
            }
        }

        [Fact]
        public void DeleteCommand_DeleteReadOnlyFileFromV3FileSystemSource()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var source = TestDirectory.Create())
            {
                //drop dummy artifacts to make it a V3
                var dummyPackageName = "foo";
                var version = "1.0.0";
                var packageFolder = Directory.CreateDirectory(Path.Combine(source.Path, dummyPackageName));
                var packageVersionFolder = Directory.CreateDirectory(Path.Combine(packageFolder.FullName, "1.0.0"));
                var dummyNuspec = Path.Combine(packageVersionFolder.FullName, dummyPackageName + ".nuspec");
                File.WriteAllText(dummyNuspec, "dummy text");
                File.SetAttributes(dummyNuspec, File.GetAttributes(dummyNuspec) | FileAttributes.ReadOnly);
                // Act
                string[] args = new string[] {
                    "delete", "foo", version,
                    "-Source", source, "-NonInteractive" };
                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    String.Join(" ", args));

                // Assert
                Assert.Equal(0, r.ExitCode);
                Assert.False(Directory.Exists(packageVersionFolder.FullName));
            }
        }

        // Same as DeleteCommand_DeleteFromFileSystemSource, except that the directory is specified
        // in unix style.
        [Fact]
        public void DeleteCommand_DeleteFromFileSystemSourceUnixStyle()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var windowsSource = TestDirectory.Create())
            {
                string source = ((string)windowsSource).Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", windowsSource);
                Assert.True(File.Exists(packageFileName));

                // Act
                string[] args = new string[] {
                    "delete",
                    "testPackage1",
                    "1.1.0",
                    "-Source",
                    source,
                    "-NonInteractive" };

                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    $"delete testPackage1 1.1.0 -Source {source} -NonInteractive");

                // Assert
                Assert.Equal(0, r.ExitCode);
                Assert.False(File.Exists(packageFileName));
            }
        }

        [Fact]
        public void DeleteCommand_DeleteFromHttpSource()
        {
            var nugetexe = Util.GetNuGetExePath();

            // Arrange
            using (var server = new MockServer())
            {
                server.Start();
                bool deleteRequestIsCalled = false;

                server.Delete.Add("/nuget/testPackage1/1.1", request =>
                {
                    deleteRequestIsCalled = true;
                    return HttpStatusCode.OK;
                });
                using SimpleTestPathContext pathContext = new SimpleTestPathContext();
                pathContext.Settings.AddSource("http-feed", $"{server.Uri}nuget");
                var configFileName = "nuget.config";
                var configFilePath = Path.Combine(pathContext.WorkingDirectory, configFileName);

                // Act
                string[] args = new string[] {
                    "delete", "testPackage1", "1.1.0",
                    "-Source", server.Uri + "nuget", "-NonInteractive", "-ConfigFile " + configFilePath };

                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args));

                // Assert
                Assert.Equal(0, r.ExitCode);
                Assert.True(deleteRequestIsCalled);
            }
        }

        [Fact]
        public void DeleteCommand_WithApiKeyAsThirdArgument()
        {
            // Arrange
            var testApiKey = Guid.NewGuid().ToString();

            using (var server = new MockServer())
            {
                server.Delete.Add("/nuget/testPackage1/1.1", r =>
                {
                    var h = r.Headers[ApiKeyHeader];
                    if (!string.Equals(h, testApiKey, StringComparison.OrdinalIgnoreCase))
                    {
                        return HttpStatusCode.Unauthorized;
                    }

                    return HttpStatusCode.OK;
                });

                server.Start();
                using SimpleTestPathContext pathContext = new SimpleTestPathContext();
                pathContext.Settings.AddSource("http-feed", $"{server.Uri}nuget");
                var configFileName = "nuget.config";
                var configFilePath = Path.Combine(pathContext.WorkingDirectory, configFileName);

                // Act
                var args = new[] {
                    "delete",
                    "testPackage1",
                    "1.1.0",
                    testApiKey,
                    "-Source",
                    server.Uri + "nuget",
                    "-NonInteractive",
                    "-ConfigFile " + configFilePath
                };

                var result = CommandRunner.Run(
                    NuGetExePath,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args));

                server.Stop();

                // Assert
                Assert.True(0 == result.ExitCode, $"{result.Output} {result.Errors}");
                Assert.Contains("testPackage1 1.1.0 was deleted successfully.", result.Output);
            }
        }

        [Fact]
        public void DeleteCommand_WithApiKeyAsNamedArgument()
        {
            // Arrange
            var testApiKey = Guid.NewGuid().ToString();

            using (var server = new MockServer())
            {
                server.Delete.Add("/nuget/testPackage1/1.1", r =>
                {
                    var h = r.Headers[ApiKeyHeader];
                    if (!string.Equals(h, testApiKey, StringComparison.OrdinalIgnoreCase))
                    {
                        return HttpStatusCode.Unauthorized;
                    }

                    return HttpStatusCode.OK;
                });

                server.Start();
                using SimpleTestPathContext pathContext = new SimpleTestPathContext();
                pathContext.Settings.AddSource("http-feed", $"{server.Uri}nuget");
                var configFileName = "nuget.config";
                var configFilePath = Path.Combine(pathContext.WorkingDirectory, configFileName);

                // Act
                var args = new[]
                {
                    "delete",
                    "testPackage1",
                    "1.1.0",
                    "should-be-ignored",  // The named argument is preferred over the positional argument.
                    "-ApiKey",
                    testApiKey,
                    "-Source",
                    server.Uri + "nuget",
                    "-NonInteractive",
                    "-ConfigFIle " + configFilePath
                };

                var result = CommandRunner.Run(
                    NuGetExePath,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args));

                server.Stop();

                // Assert
                Assert.True(0 == result.ExitCode, $"{result.Output} {result.Errors}");
                Assert.Contains("testPackage1 1.1.0 was deleted successfully.", result.Output);
            }
        }

        [Theory]
        [InlineData("{0}index.json")] // package source url
        [InlineData("{0}push")] // delete package endpoint
        public void DeleteCommand_WithApiKeyFromConfig(string configKeyFormatString)
        {
            // Arrange
            var testApiKey = Guid.NewGuid().ToString();

            using (var pathContext = new SimpleTestPathContext())
            using (var server = new MockServer())
            {
                // Server setup
                var indexJson = Util.CreateIndexJson();

                Util.AddFlatContainerResource(indexJson, server);
                Util.AddPublishResource(indexJson, server);

                server.Get.Add("/index.json", r =>
                {
                    return new Action<HttpListenerResponse>(response =>
                    {
                        response.StatusCode = 200;
                        response.ContentType = "text/javascript";
                        MockServer.SetResponseContent(response, indexJson.ToString());
                    });
                });

                server.Delete.Add("/push/testPackage1/1.1", r =>
                {
                    var h = r.Headers[ApiKeyHeader];
                    if (!string.Equals(h, testApiKey, StringComparison.OrdinalIgnoreCase))
                    {
                        return HttpStatusCode.Unauthorized;
                    }
                    return HttpStatusCode.OK;
                });

                server.Start();

                // Add the source and apikeys into NuGet.Config file
                var settings = pathContext.Settings;
                SimpleTestSettingsContext.RemoveSource(settings.XML, "source");

                var source = server.Uri + "index.json";
                var packageSourcesSection = SimpleTestSettingsContext.GetOrAddSection(settings.XML, "packageSources");
                SimpleTestSettingsContext.AddEntry(packageSourcesSection, $"MockServer", source, "AllowInsecureConnections", "true");

                var configKey = string.Format(configKeyFormatString, server.Uri);
                var configValue = Configuration.EncryptionUtility.EncryptString(testApiKey);
                var apikeysSection = SimpleTestSettingsContext.GetOrAddSection(settings.XML, "apikeys");
                SimpleTestSettingsContext.AddEntry(apikeysSection, configKey, configValue);
                settings.Save();

                // Act
                var args = new[]
                    {
                        "delete",
                        "testPackage1",
                        "1.1.0",
                        "-Source",
                        "MockServer",
                        "-ConfigFile",
                        pathContext.NuGetConfig,
                        "-NonInteractive"
                    };

                var result = CommandRunner.Run(
                    NuGetExePath,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args));

                server.Stop();

                // Assert
                Assert.True(0 == result.ExitCode, $"{result.Output} {result.Errors}");
                Assert.Contains("testPackage1 1.1.0 was deleted successfully.", result.Output);
            }
        }

        [Theory, MemberData(nameof(ServerWarningData))]
        public void DeleteCommand_ShowsServerWarnings(string firstServerWarning, string secondServerWarning)
        {
            var serverWarnings = new[] { firstServerWarning, secondServerWarning };
            var nugetexe = Util.GetNuGetExePath();

            // Arrange
            using (var server = new MockServer())
            {
                server.Start();
                using SimpleTestPathContext pathContext = new SimpleTestPathContext();
                pathContext.Settings.AddSource("http-feed", $"{server.Uri}nuget");
                var configFileName = "nuget.config";
                var configFilePath = Path.Combine(pathContext.WorkingDirectory, configFileName);
                server.Delete.Add("/nuget/testPackage1/1.1", request => HttpStatusCode.OK);

                server.AddServerWarnings(serverWarnings);

                // Act
                string[] args = new string[] {
                    "delete", "testPackage1", "1.1.0",
                    "-Source", server.Uri + "nuget", "-NonInteractive", "-ConfigFile " + configFilePath };

                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args));

                // Assert
                foreach (var serverWarning in serverWarnings)
                {
                    if (!string.IsNullOrEmpty(serverWarning))
                    {
                        Assert.Contains(serverWarning, r.Output);
                    }
                }
            }
        }

        [Theory]
        [InlineData("delete Someting")]
        [InlineData("delete a b c d")]
        public void DeleteCommand_Failure_InvalidArguments(string args)
        {
            Util.TestCommandInvalidArguments(args);
        }

        [Theory]
        [InlineData("true", false)]
        [InlineData("false", true)]
        public void DeleteCommand_WhenDeleteWithHttpSourceAndAllowInsecureConnections_DisplayesErrorCorrectly(string allowInsecureConnections, bool shouldFail)
        {
            var nugetexe = Util.GetNuGetExePath();

            // Arrange
            using (var server = new MockServer())
            {
                server.Start();
                bool deleteRequestIsCalled = false;

                server.Delete.Add("/nuget/testPackage1/1.1", request =>
                {
                    deleteRequestIsCalled = true;
                    return HttpStatusCode.OK;
                });

                using SimpleTestPathContext config = new SimpleTestPathContext();

                // Arrange the NuGet.Config file
                string nugetConfigContent =
    $@"<configuration>
    <packageSources>
        <clear />
        <add key='http-feed' value='{server.Uri}nuget' protocalVersion=""3"" allowInsecureConnections=""{allowInsecureConnections}"" />
    </packageSources>
</configuration>";
                File.WriteAllText(config.NuGetConfig, nugetConfigContent);
                string expectedError = string.Format(CultureInfo.CurrentCulture, _httpErrorSingle, "delete", $"{server.Uri}nuget");

                // Act
                string[] args = new string[] {
                    "delete", "testPackage1", "1.1.0",
                    "-Source", server.Uri + "nuget",
                    "-ConfigFile", config.NuGetConfig, "-NonInteractive" };

                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args));

                // Assert
                if (shouldFail)
                {
                    Assert.Equal(1, result.ExitCode);
                    Assert.Contains(expectedError, result.AllOutput);
                }
                else
                {
                    Assert.Equal(0, result.ExitCode);
                    Assert.True(deleteRequestIsCalled);
                    Assert.DoesNotContain(expectedError, result.AllOutput);
                }
            }
        }

        public static IEnumerable<object[]> ServerWarningData
        {
            get
            {
                return new[]
                {
                    new object[] { null, null },
                    new object[] { "Single server warning message", null},
                    new object[] { "First of two server warning messages", "Second of two server warning messages"}
                };
            }
        }
    }
}
