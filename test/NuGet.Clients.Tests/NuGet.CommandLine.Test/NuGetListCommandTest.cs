// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using FluentAssertions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetListCommandTest
    {
        string _httpErrorSingle = "You are running the '{0}' operation with an 'HTTP' source: {1}. NuGet requires HTTPS sources. To use an HTTP source, you must explicitly set 'allowInsecureConnections' to true in your NuGet.Config file. Please refer to https://aka.ms/nuget-https-everywhere.";
        string _httpErrorMultiple = "You are running the '{0}' operation with 'HTTP' sources: {1}. NuGet requires HTTPS sources. To use HTTP sources, you must explicitly set 'allowInsecureConnections' to true in your NuGet.Config file. Please refer to https://aka.ms/nuget-https-everywhere.";

        [Fact]
        public void ListCommand_WithNugetShowStack_ShowsStack()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();
            var hostName = Guid.NewGuid().ToString();
            var fullHostName = "https://" + hostName + "/";
            var expected = "NuGet.Protocol.Core.Types.FatalProtocolException: Unable to load the service index for source " +
                           $"{fullHostName}";

            var args = new[] { "list", "-Source", fullHostName };

            // Act
            var result = CommandRunner.Run(
                nugetexe,
                Directory.GetCurrentDirectory(),
                string.Join(" ", args),
                environmentVariables: new Dictionary<string, string>
                {
                    { "NUGET_SHOW_STACK", "true" }
                });

            // Assert
            Assert.Contains(expected, result.Output + " " + result.Errors);
            Assert.NotEqual(0, result.ExitCode);
        }

        [Fact]
        public void ListCommand_WithoutNugetShowStack_HidesStack()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();
            var hostName = Guid.NewGuid().ToString();
            var expected = $"The remote name could not be resolved: '{hostName}'";

            if (RuntimeEnvironmentHelper.IsMono)
            {
                expected = "No such host is known";
            }

            var args = new[] { "list", "-Source", "https://" + hostName + "/" };

            // Act
            var result = CommandRunner.Run(
                nugetexe,
                Directory.GetCurrentDirectory(),
                string.Join(" ", args),
                environmentVariables: new Dictionary<string, string>
                {
                    { "NUGET_SHOW_STACK", "false" }
                });

            // Assert
            Assert.Contains(expected, result.Output + " " + result.Errors);
            Assert.NotEqual(0, result.ExitCode);
        }

        [Fact]
        public void ListCommand_WithUserSpecifiedSource()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var repositoryPath = TestDirectory.Create())
            {
                Util.CreateTestPackage("testPackage1", "1.1.0", repositoryPath);
                Util.CreateTestPackage("testPackage2", "2.0.0", repositoryPath);

                string[] args = new string[] { "list", "-Source", repositoryPath };

                // Act
                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args));

                // Assert
                Assert.Equal(0, result.ExitCode);
                var output = RemoveDeprecationWarning(result.Output);
                Assert.Equal($"testPackage1 1.1.0{Environment.NewLine}testPackage2 2.0.0{Environment.NewLine}", output);
            }
        }

        [Fact]
        public void ListCommand_ShowLicenseUrlWithDetailedVerbosity()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var repositoryPath = TestDirectory.Create())
            {
                Util.CreateTestPackage("testPackage1", "1.1.0", repositoryPath, new Uri("http://kaka"));

                string[] args = new string[] { "list", "-Source", repositoryPath, "-verbosity", "detailed" };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args));

                // Assert
                Assert.Equal(0, r.ExitCode);
                var output = RemoveDeprecationWarning(r.Output);
                string[] lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                Assert.Equal(5, lines.Length);
                Assert.Equal("testPackage1", lines[1]);
                Assert.Equal(" 1.1.0", lines[2]);
                Assert.Equal(" desc of testPackage1 1.1.0", lines[3]);
                Assert.Equal(" License url: http://kaka/", lines[4]);
            }
        }

        [Fact]
        public void ListCommand_WithUserSpecifiedConfigFile()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var randomFolder = TestDirectory.Create())
            using (var repositoryPath = TestDirectory.Create())
            {
                Util.CreateTestPackage("testPackage1", "1.1.0", repositoryPath);
                Util.CreateTestPackage("testPackage2", "2.0.0", repositoryPath);

                // create the config file
                Util.CreateFile(randomFolder, "nuget.config", "<configuration/>");
                var configFile = Path.Combine(randomFolder, "nuget.config");

                string[] args = new string[] {
                "sources",
                "Add",
                "-Name",
                "test_source",
                "-Source",
                repositoryPath,
                "-ConfigFile",
                Path.Combine(randomFolder, "nuget.config")
            };
                int r = Program.Main(args);
                Assert.Equal(0, r);

                // Act: execute the list command
                args = new string[] { "list", "-Source", "test_source", "-ConfigFile", configFile };

                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args));

                // Assert
                Assert.Equal(0, result.ExitCode);
                var output = RemoveDeprecationWarning(result.Output);
                Assert.Equal($"testPackage1 1.1.0{Environment.NewLine}testPackage2 2.0.0{Environment.NewLine}", output);
            }
        }

        // Tests list command, with no other switches
        [Fact]
        public void ListCommand_Simple()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            using (SimpleTestPathContext config = new SimpleTestPathContext())
            {
                // Arrange
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new FileInfo(packageFileName1);
                var package2 = new FileInfo(packageFileName2);

                using (var server = new MockServer())
                {
                    string searchRequest = string.Empty;

                    server.Get.Add("/nuget/$metadata", r =>
                        Util.GetMockServerResource());
                    server.Get.Add("/nuget/Search()", r =>
                        new Action<HttpListenerResponse>(response =>
                        {
                            searchRequest = r.Url.ToString();
                            response.ContentType = "application/atom+xml;type=feed;charset=utf-8";
                            string feed = server.ToODataFeed(new[] { package1, package2 }, "Search");
                            MockServer.SetResponseContent(response, feed);
                        }));
                    server.Get.Add("/nuget", r => "OK");

                    server.Start();
                    config.Settings.AddSource("mockServer", $"{server.Uri}nuget", allowInsecureConnectionsValue: "true");

                    // Act
                    var args = "list test -Source " + server.Uri + "nuget";
                    var result = CommandRunner.Run(
                        nugetexe,
                        config.WorkingDirectory,
                        args);
                    server.Stop();

                    // Assert
                    Assert.Equal(0, result.ExitCode);

                    // verify that only package id & version is displayed
                    var expectedOutput = "testPackage1 1.1.0" + Environment.NewLine +
                        "testPackage2 2.1.0" + Environment.NewLine;
                    Assert.Equal(expectedOutput, RemoveDeprecationWarning(result.Output));

                    Assert.Contains("$filter=IsLatestVersion", searchRequest);
                    Assert.Contains("searchTerm='test", searchRequest);
                    Assert.Contains("includePrerelease=false", searchRequest);
                }
            }
        }

        // Tests that list command only show listed packages
        [Fact]
        public void ListCommand_OnlyShowListed()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            using (SimpleTestPathContext config = new SimpleTestPathContext())
            {
                // Arrange
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new FileInfo(packageFileName1);
                var package2 = new FileInfo(packageFileName2);

                using (var server = new MockServer())
                {
                    string searchRequest = string.Empty;

                    server.Get.Add("/nuget/$metadata", r =>
                        Util.GetMockServerResource());
                    server.Get.Add("/nuget/Search()", r =>
                        new Action<HttpListenerResponse>(response =>
                        {
                            searchRequest = r.Url.ToString();
                            response.ContentType = "application/atom+xml;type=feed;charset=utf-8";
                            string feed = server.ToODataFeed(new[] { (package1, false, new DateTimeOffset(new DateTime(1800, 1, 1))), (package2, true, DateTimeOffset.Now) }, "Search");
                            MockServer.SetResponseContent(response, feed);
                        }));
                    server.Get.Add("/nuget", r => "OK");

                    server.Start();
                    config.Settings.AddSource("mockServer", $"{server.Uri}nuget", allowInsecureConnectionsValue: "true");

                    // Act
                    var args = "list test -Source " + server.Uri + "nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        config.WorkingDirectory,
                        args);
                    server.Stop();

                    // Assert
                    Assert.True(r1.ExitCode == 0, r1.Output + " " + r1.Errors);

                    // verify that only testPackage2 is listed since the package testPackage1
                    // is not listed.
                    var expectedOutput = "testPackage2 2.1.0" + Environment.NewLine;
                    Assert.Equal(expectedOutput, RemoveDeprecationWarning(r1.Output));

                    Assert.Contains("$filter=IsLatestVersion", searchRequest);
                    Assert.Contains("searchTerm='test", searchRequest);
                    Assert.Contains("includePrerelease=false", searchRequest);

                    // verify that nuget doesn't include "includeDelisted" in its request
                    Assert.DoesNotContain("includeDelisted", searchRequest);
                }
            }
        }

        // Tests that list command show delisted packages
        // when IncludeDelisted is specified.
        [Fact]
        public void ListCommand_IncludeDelisted()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            using (SimpleTestPathContext config = new SimpleTestPathContext())
            {
                // Arrange
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new FileInfo(packageFileName1);
                var package2 = new FileInfo(packageFileName2);

                using (var server = new MockServer())
                {
                    string searchRequest = string.Empty;

                    server.Get.Add("/nuget/$metadata", r =>
                        Util.GetMockServerResource());
                    server.Get.Add("/nuget/Search()", r =>
                        new Action<HttpListenerResponse>(response =>
                        {
                            searchRequest = r.Url.ToString();
                            response.ContentType = "application/atom+xml;type=feed;charset=utf-8";
                            string feed = server.ToODataFeed(new[] { (package1, false, DateTimeOffset.Now), (package2, true, DateTimeOffset.Now) }, "Search");
                            MockServer.SetResponseContent(response, feed);
                        }));
                    server.Get.Add("/nuget", r => "OK");

                    server.Start();
                    config.Settings.AddSource("mockServer", $"{server.Uri}nuget", allowInsecureConnectionsValue: "true");

                    // Act
                    var args = "list test -IncludeDelisted -Source " + server.Uri + "nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        config.WorkingDirectory,
                        args);
                    server.Stop();

                    // Assert
                    Assert.True(r1.ExitCode == 0, r1.Output + " " + r1.Errors);

                    // verify that both testPackage1 and testPackage2 are listed.
                    var expectedOutput =
                        "testPackage1 1.1.0" + Environment.NewLine +
                        "testPackage2 2.1.0" + Environment.NewLine;
                    Assert.Equal(expectedOutput, RemoveDeprecationWarning(r1.Output));

                    Assert.Contains("$filter=IsLatestVersion", searchRequest);
                    Assert.Contains("searchTerm='test", searchRequest);
                    Assert.Contains("includePrerelease=false", searchRequest);
                }
            }
        }

        // Tests that list command displays detailed package info when -Verbosity is detailed.
        [Fact]
        public void ListCommand_VerboseOutput()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            using (SimpleTestPathContext config = new SimpleTestPathContext())
            {
                // Arrange
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new FileInfo(packageFileName1);
                var package2 = new FileInfo(packageFileName2);

                using (var server = new MockServer())
                {
                    string searchRequest = string.Empty;

                    server.Get.Add("/nuget/$metadata", r =>
                        Util.GetMockServerResource());
                    server.Get.Add("/nuget/Search()", r =>
                        new Action<HttpListenerResponse>(response =>
                        {
                            searchRequest = r.Url.ToString();
                            response.ContentType = "application/atom+xml;type=feed;charset=utf-8";
                            string feed = server.ToODataFeed(new[] { package1, package2 }, "Search");
                            MockServer.SetResponseContent(response, feed);
                        }));
                    server.Get.Add("/nuget", r => "OK");

                    server.Start();
                    config.Settings.AddSource("mockServer", $"{server.Uri}nuget", allowInsecureConnectionsValue: "true");

                    // Act
                    var args = "list test -Verbosity detailed -Source " + server.Uri + "nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        config.WorkingDirectory,
                        args);
                    server.Stop();

                    // Assert
                    Assert.Equal(0, r1.ExitCode);

                    // verify that the output is detailed
                    Assert.Contains(new PackageArchiveReader(package1.OpenRead()).NuspecReader.GetDescription(), r1.Output);
                    Assert.Contains(new PackageArchiveReader(package2.OpenRead()).NuspecReader.GetDescription(), r1.Output);

                    Assert.Contains("$filter=IsLatestVersion", searchRequest);
                    Assert.Contains("searchTerm='test", searchRequest);
                    Assert.Contains("includePrerelease=false", searchRequest);
                }
            }
        }

        // Tests that when -AllVersions is specified, list command sends request
        // without $filter
        [Fact]
        public void ListCommand_AllVersions()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            using (SimpleTestPathContext config = new SimpleTestPathContext())
            {
                // Arrange
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new FileInfo(packageFileName1);
                var package2 = new FileInfo(packageFileName2);

                using (var server = new MockServer())
                {
                    string searchRequest = string.Empty;

                    server.Get.Add("/nuget/$metadata", r =>
                        Util.GetMockServerResource());
                    server.Get.Add("/nuget/Search()", r =>
                        new Action<HttpListenerResponse>(response =>
                        {
                            searchRequest = r.Url.ToString();
                            response.ContentType = "application/atom+xml;type=feed;charset=utf-8";
                            string feed = server.ToODataFeed(new[] { package1, package2 }, "Search");
                            MockServer.SetResponseContent(response, feed);
                        }));
                    server.Get.Add("/nuget", r => "OK");

                    server.Start();
                    config.Settings.AddSource("mockServer", $"{server.Uri}nuget", allowInsecureConnectionsValue: "true");

                    // Act
                    var args = "list test -AllVersions -Source " + server.Uri + "nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        config.WorkingDirectory,
                        args);
                    server.Stop();

                    // Assert
                    Assert.Equal(0, r1.ExitCode);

                    // verify that the output is detailed
                    var expectedOutput = "testPackage1 1.1.0" + Environment.NewLine +
                        "testPackage2 2.1.0" + Environment.NewLine;
                    Assert.Equal(expectedOutput, RemoveDeprecationWarning(r1.Output));

                    Assert.DoesNotContain("$filter", searchRequest);
                    Assert.Contains("searchTerm='test", searchRequest);
                    Assert.Contains("includePrerelease=false", searchRequest);
                }
            }
        }

        // Test case when switch -Prerelease is specified
        [Fact]
        public void ListCommand_Prerelease()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                var packageDirectory = Path.Combine(pathContext.WorkingDirectory, "packageFolder");
                Directory.CreateDirectory(packageDirectory);
                var solutionFolder = Path.Combine(pathContext.SolutionRoot);

                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new FileInfo(packageFileName1);
                var package2 = new FileInfo(packageFileName2);

                using (var server = new MockServer())
                {
                    string searchRequest = string.Empty;

                    server.Get.Add("/nuget/$metadata", r =>
                        Util.GetMockServerResource());
                    server.Get.Add("/nuget/Search()", r =>
                        new Action<HttpListenerResponse>(response =>
                        {
                            searchRequest = r.Url.ToString();
                            response.ContentType = "application/atom+xml;type=feed;charset=utf-8";
                            string feed = server.ToODataFeed(new[] { package1, package2 }, "Search");
                            MockServer.SetResponseContent(response, feed);
                        }));
                    server.Get.Add("/nuget", r => "OK");

                    server.Start();
                    pathContext.Settings.AddSource("mockServer", $"{server.Uri}nuget", allowInsecureConnectionsValue: "true");

                    // Act
                    var args = "list test -Prerelease -Source " + server.Uri + "nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        solutionFolder,
                        args);
                    server.Stop();

                    // Assert
                    Assert.Equal(0, r1.ExitCode);

                    // verify that the output is detailed
                    var expectedOutput = "testPackage1 1.1.0" + Environment.NewLine +
                        "testPackage2 2.1.0" + Environment.NewLine;
                    Assert.Equal(expectedOutput, RemoveDeprecationWarning(r1.Output));

                    Assert.Contains("$filter=IsAbsoluteLatestVersion", searchRequest);
                    Assert.Contains("searchTerm='test", searchRequest);
                    Assert.Contains("includePrerelease=true", searchRequest);
                }
            }
        }

        // Test case when both switches -Prerelease and -AllVersions are specified
        [Fact]
        public void ListCommand_AllVersionsPrerelease()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                var packageDirectory = Path.Combine(pathContext.WorkingDirectory, "packageFolder");
                Directory.CreateDirectory(packageDirectory);
                var solutionFolder = Path.Combine(pathContext.SolutionRoot);

                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new FileInfo(packageFileName1);
                var package2 = new FileInfo(packageFileName2);

                using (var server = new MockServer())
                {
                    string searchRequest = string.Empty;

                    server.Get.Add("/nuget/$metadata", r =>
                        Util.GetMockServerResource());
                    server.Get.Add("/nuget/Search()", r =>
                        new Action<HttpListenerResponse>(response =>
                        {
                            searchRequest = r.Url.ToString();
                            response.ContentType = "application/atom+xml;type=feed;charset=utf-8";
                            string feed = server.ToODataFeed(new[] { package1, package2 }, "Search");
                            MockServer.SetResponseContent(response, feed);
                        }));
                    server.Get.Add("/nuget", r => "OK");

                    server.Start();
                    pathContext.Settings.AddSource("mockServer", $"{server.Uri}nuget", allowInsecureConnectionsValue: "true");

                    // Act
                    var args = "list test -AllVersions -Prerelease -Source " + server.Uri + "nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        solutionFolder,
                        args);
                    server.Stop();

                    // Assert
                    Assert.Equal(0, r1.ExitCode);

                    // verify that the output is detailed
                    var expectedOutput = "testPackage1 1.1.0" + Environment.NewLine +
                        "testPackage2 2.1.0" + Environment.NewLine;
                    RemoveDeprecationWarning(r1.Output).Should().Be(expectedOutput);

                    Assert.DoesNotContain("$filter", searchRequest);
                    Assert.Contains("searchTerm='test", searchRequest);
                    Assert.Contains("includePrerelease=true", searchRequest);
                }
            }
        }

        [Fact]
        public void ListCommand_SimpleV3()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                var packageDirectory = Path.Combine(pathContext.WorkingDirectory, "packageFolder");
                Directory.CreateDirectory(packageDirectory);

                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new FileInfo(packageFileName1);
                var package2 = new FileInfo(packageFileName2);

                // Server setup
                var indexJson = Util.CreateIndexJson();
                using (var serverV3 = new MockServer())
                {
                    serverV3.Get.Add("/", r =>
                    {
                        var path = serverV3.GetRequestUrlAbsolutePath(r);

                        if (path == "/index.json")
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.StatusCode = 200;
                                response.ContentType = "text/javascript";
                                MockServer.SetResponseContent(response, indexJson.ToString());
                            });
                        }

                        throw new Exception("This test needs to be updated to support: " + path);
                    });

                    using (var serverV2 = new MockServer())
                    {
                        Util.AddFlatContainerResource(indexJson, serverV3);
                        Util.AddLegacyGalleryResource(indexJson, serverV2);
                        string searchRequest = string.Empty;

                        serverV2.Get.Add("/", r =>
                        {
                            var path = serverV2.GetRequestUrlAbsolutePath(r);

                            if (path == "/")
                            {
                                return "OK";
                            }

                            if (path == "/$metadata")
                            {
                                return Util.GetMockServerResource();
                            }

                            if (path == "/Search()")
                            {
                                return new Action<HttpListenerResponse>(response =>
                                {
                                    searchRequest = r.Url.ToString();
                                    response.ContentType = "application/atom+xml;type=feed;charset=utf-8";
                                    string feed = serverV2.ToODataFeed(new[] { package1, package2 }, "Search");
                                    MockServer.SetResponseContent(response, feed);
                                });
                            }

                            throw new Exception("This test needs to be updated to support: " + path);
                        });

                        serverV3.Start();
                        serverV2.Start();
                        pathContext.Settings.AddSource("mockServer", $"{serverV3.Uri}index.json", allowInsecureConnectionsValue: "true");

                        // Act
                        var args = "list test -Source " + serverV3.Uri + "index.json";
                        var result = CommandRunner.Run(
                            nugetexe,
                            pathContext.SolutionRoot,
                            args);

                        serverV2.Stop();
                        serverV3.Stop();

                        // Assert
                        Assert.True(result.ExitCode == 0, result.Output + " " + result.Errors);

                        // verify that only package id & version is displayed
                        var expectedOutput = "testPackage1 1.1.0" + Environment.NewLine +
                            "testPackage2 2.1.0" + Environment.NewLine;
                        Assert.Equal(expectedOutput, RemoveDeprecationWarning(result.Output));

                        Assert.Contains("$filter=IsLatestVersion", searchRequest);
                        Assert.Contains("searchTerm='test", searchRequest);
                        Assert.Contains("includePrerelease=false", searchRequest);
                    }
                }
            }
        }

        [Fact]
        public void ListCommand_SimpleV3_NoListEndpoint()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                // Server setup
                var packageDirectory = Path.Combine(pathContext.WorkingDirectory, "packageFolder");
                Directory.CreateDirectory(packageDirectory);

                var indexJson = Util.CreateIndexJson();
                using (var serverV3 = new MockServer())
                {
                    serverV3.Get.Add("/", r =>
                    {
                        var path = serverV3.GetRequestUrlAbsolutePath(r);

                        if (path == "/index.json")
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.StatusCode = 200;
                                response.ContentType = "text/javascript";
                                MockServer.SetResponseContent(response, indexJson.ToString());
                            });
                        }

                        throw new Exception("This test needs to be updated to support: " + path);
                    });

                    serverV3.Start();
                    pathContext.Settings.AddSource("mockServer", $"{serverV3.Uri}index.json", allowInsecureConnectionsValue: "true");

                    // Act
                    var args = "list test -Source " + serverV3.Uri + "index.json";
                    var result = CommandRunner.Run(
                        nugetexe,
                        pathContext.SolutionRoot,
                        args);

                    serverV3.Stop();

                    // Assert
                    Assert.True(result.ExitCode == 0, result.Output + " " + result.Errors);

                    // verify that only package id & version is displayed
                    var expectedOutput =
                        string.Format(
                      "WARNING: This version of nuget.exe does not support listing packages" +
                      " from package source '{0}'.",
                      serverV3.Uri + "index.json");

                    // Verify that the output contains the expected output
                    Assert.True(result.Output.Contains(expectedOutput));
                }
            }
        }

        [Fact]
        public void ListCommand_UnavailableV3()
        {
            var nugetexe = Util.GetNuGetExePath();
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                // Server setup
                using (var serverV3 = new MockServer())
                {
                    serverV3.Get.Add("/", r =>
                    {
                        var path = serverV3.GetRequestUrlAbsolutePath(r);

                        if (path == "/index.json")
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.StatusCode = 404;
                                response.ContentType = "text/javascript";
                                MockServer.SetResponseContent(response, response.StatusCode.ToString());
                            });
                        }

                        throw new Exception("This test needs to be updated to support: " + path);
                    });

                    serverV3.Start();
                    pathContext.Settings.AddSource("mockServer", $"{serverV3.Uri}index.json", allowInsecureConnectionsValue: "true");

                    // Act
                    var args = "list test -Source " + serverV3.Uri + "index.json";
                    var result = CommandRunner.Run(
                        nugetexe,
                        pathContext.SolutionRoot,
                        args);

                    serverV3.Stop();

                    // Assert
                    Assert.True(result.ExitCode != 0, result.Output + " " + result.Errors);

                    Assert.True(
                        result.Errors.Contains("404 (Not Found)"),
                        "Expected error message not found in " + result.Errors
                        );
                }
            }
        }

        [Theory]
        [InlineData("invalid")]
        public void ListCommand_InvalidInput_NonSource(string invalidInput)
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();
            using (var pathContext = new SimpleTestPathContext())
            {
                // Act
                var args = "list test -Source " + invalidInput;
                var result = CommandRunner.Run(
                    nugetexe,
                    pathContext.SolutionRoot,
                    args);

                // Assert
                Assert.True(
                    result.ExitCode != 0,
                    "The run did not fail as desired. Simply got this output:" + result.Output);

                Assert.True(
                    result.Errors.Contains(
                        string.Format(
                            "The specified source '{0}' is invalid. Please provide a valid source.",
                            invalidInput)),
                    "Expected error message not found in " + result.Errors
                    );
            }
        }

        [Theory]
        [InlineData("https://invalid-2a0358f1-88f2-48c0-b68a-bb150cac00bd.org")]
        [InlineData("https://invalid-2a0358f1-88f2-48c0-b68a-bb150cac00bd.org/api/v2")]
        public void ListCommand_InvalidInput_V2_NonExistent(string invalidInput)
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            // Act
            using (var pathContext = new SimpleTestPathContext())
            {
                var args = "list test -Source " + invalidInput;
                var result = CommandRunner.Run(
                    nugetexe,
                    pathContext.SolutionRoot,
                    args);

                // Assert
                Assert.True(
                    result.ExitCode != 0,
                    "The run did not fail as desired. Simply got this output:" + result.Output);

                Assert.Contains($"Unable to load the service index for source {invalidInput}.", result.Errors);
            }
        }

        [Theory]
        [InlineData("https://nuget.org/api/blah")]
        public void ListCommand_InvalidInput_V2_NotFound(string invalidInput)
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            // Act
            var args = "list test -Source " + invalidInput;
            var result = CommandRunner.Run(
                nugetexe,
                Directory.GetCurrentDirectory(),
                args);

            // Assert
            Assert.True(
                result.ExitCode != 0,
                "The run did not fail as desired. Simply got this output:" + result.Output);

            Assert.True(
                result.Errors.Contains(
                    "returned an unexpected status code '404 Not Found'."),
                "Expected error message not found in:\n " + result.Errors
                );
        }

        [Theory]
        [InlineData("https://invalid-2a0358f1-88f2-48c0-b68a-bb150cac00bd.org/v3/index.json")]
        public void ListCommand_InvalidInput_V3_NonExistent(string invalidInput)
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            // Act
            using (var pathContext = new SimpleTestPathContext())
            {
                var args = "list test -Source " + invalidInput;
                var result = CommandRunner.Run(
                    nugetexe,
                    pathContext.SolutionRoot,
                    args);

                // Assert
                Assert.True(
                    result.ExitCode != 0,
                    "The run did not fail as desired. Simply got this output:" + result.Output);

                Assert.True(
                    result.Errors.Contains($"Unable to load the service index for source {invalidInput}."),
                    "Expected error message not found in " + result.Errors
                    );
            }
        }

        [Theory]
        [InlineData("https://api.nuget.org/v4/index.json")]
        public void ListCommand_InvalidInput_V3_NotFound(string invalidInput)
        {
            // Arrange
            string nugetexe = Util.GetNuGetExePath();

            // Act
            using (var pathContext = new SimpleTestPathContext())
            {
                var args = "list test -Source " + invalidInput;
                CommandRunnerResult result = CommandRunner.Run(
                    filename: nugetexe,
                    workingDirectory: pathContext.SolutionRoot,
                    arguments: args);

                // Assert
                Assert.False(
                    result.Success,
                    "The run did not fail as desired. Simply got this output:" + result.Output);

                Assert.True(
                    result.Errors.Contains("Response status code does not indicate success"),
                    "Expected error message not found in " + result.Errors
                    );
            }
        }

        [Fact]
        public void ListCommand_WithAuthenticatedSource_AppliesCredentialsFromSettings()
        {
            var expectedAuthHeader = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user:password"));
            var listEndpoint = Guid.NewGuid().ToString() + "/api/v2";
            bool serverReceiveProperAuthorizationHeader = false;

            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                var repo = Path.Combine(pathContext.WorkingDirectory, "repo");
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", repo);
                var package1 = new FileInfo(packageFileName1);

                // Server setup
                using (var serverV3 = new MockServer())
                {
                    var indexJson = Util.CreateIndexJson();
                    Util.AddFlatContainerResource(indexJson, serverV3);
                    Util.AddLegacyGalleryResource(indexJson, serverV3, listEndpoint);

                    serverV3.Get.Add("/", r =>
                    {
                        var h = r.Headers["Authorization"];
                        if (h == null)
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.StatusCode = 401;
                                response.AddHeader("WWW-Authenticate", @"Basic realm=""Test""");
                                MockServer.SetResponseContent(response, "401 Unauthenticated");
                            });
                        }

                        if (expectedAuthHeader != h)
                        {
                            return HttpStatusCode.Forbidden;
                        }

                        var path = serverV3.GetRequestUrlAbsolutePath(r);

                        if (path == "/index.json")
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.StatusCode = 200;
                                response.ContentType = "text/javascript";
                                MockServer.SetResponseContent(response, indexJson.ToString());
                            });
                        }

                        if (path == $"/{listEndpoint}/$metadata")
                        {
                            return Util.GetMockServerResource();
                        }

                        if (path == $"/{listEndpoint}/Search()")
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.ContentType = "application/atom+xml;type=feed;charset=utf-8";
                                var feed = serverV3.ToODataFeed(new[] { package1 }, "Search");
                                MockServer.SetResponseContent(response, feed);
                            });
                        }

                        serverReceiveProperAuthorizationHeader = true;
                        return "OK";
                    });

                    // Add source into NuGet.Config file
                    var settings = pathContext.Settings;
                    SimpleTestSettingsContext.RemoveSource(settings.XML, "source");

                    var source = serverV3.Uri + "index.json";
                    var packageSourcesSection = SimpleTestSettingsContext.GetOrAddSection(settings.XML, "packageSources");
                    SimpleTestSettingsContext.AddEntry(packageSourcesSection, "vsts", source, additionalAtrributeName: "protocolVersion", additionalAttributeValue: "3", additionalAtrributeName2: "allowInsecureConnections", additionalAttributeValue2: "true");

                    //var packageSourceCredentialsSection = SimpleTestSettingsContext.GetOrAddSection(settings.XML, "packageSourceCredentials");
                    SimpleTestSettingsContext.AddPackageSourceCredentialsSection(settings.XML, "vsts", "user", "password", clearTextPassword: true);
                    settings.Save();

                    serverV3.Start();

                    // Act
                    var result = CommandRunner.Run(
                        Util.GetNuGetExePath(),
                        pathContext.SolutionRoot,
                        $"list test -source {serverV3.Uri}index.json -configfile {pathContext.NuGetConfig} -verbosity detailed -noninteractive");

                    serverV3.Stop();

                    // Assert
                    Assert.True(0 == result.ExitCode, $"{result.Output} {result.Errors}");
                    Assert.True(serverReceiveProperAuthorizationHeader);
                    Assert.Contains($"GET {serverV3.Uri}{listEndpoint}/Search()", result.Output);
                    // verify that only package id & version is displayed
                    Assert.Matches(@"(?m)testPackage1\s+1\.1\.0", result.Output);

                }
            }
        }

        [Fact]
        public void ListCommand_WithAuthenticatedSourceV2_AppliesCredentialsFromSettings()
        {
            var expectedAuthHeader = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user:password"));
            var listEndpoint = "api/v2";
            bool serverReceiveProperAuthorizationHeader = false;
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                var repo = Path.Combine(pathContext.WorkingDirectory, "repo");
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", repo);
                var package1 = new FileInfo(packageFileName1);

                // Server setup
                using (var serverV3 = new MockServer())
                {
                    //var indexJson = Util.CreateIndexJson();
                    //Util.AddFlatContainerResource(indexJson, serverV3);
                    //Util.AddLegacyGalleryResource(indexJson, serverV3, listEndpoint);

                    serverV3.Get.Add("/", r =>
                    {
                        var h = r.Headers["Authorization"];
                        if (h == null)
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.StatusCode = 401;
                                response.AddHeader("WWW-Authenticate", @"Basic realm=""Test""");
                                MockServer.SetResponseContent(response, "401 Unauthenticated");
                            });
                        }

                        if (expectedAuthHeader != h)
                        {
                            return HttpStatusCode.Forbidden;
                        }

                        var path = serverV3.GetRequestUrlAbsolutePath(r);

                        //if (path == "/index.json")
                        //{
                        //    return new Action<HttpListenerResponse>(response =>
                        //    {
                        //        response.StatusCode = 200;
                        //        response.ContentType = "text/javascript";
                        //        MockServer.SetResponseContent(response, indexJson.ToString());
                        //    });
                        //}

                        if (path == $"/{listEndpoint}/$metadata")
                        {
                            return Util.GetMockServerResource();
                        }

                        if (path == $"/{listEndpoint}/Search()")
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.ContentType = "application/atom+xml;type=feed;charset=utf-8";
                                var feed = serverV3.ToODataFeed(new[] { package1 }, "Search");
                                MockServer.SetResponseContent(response, feed);
                            });
                        }

                        serverReceiveProperAuthorizationHeader = true;
                        return "OK";
                    });

                    // Add source into NuGet.Config file
                    var settings = pathContext.Settings;
                    SimpleTestSettingsContext.RemoveSource(settings.XML, "source");

                    var source = $"{serverV3.Uri}api/v2";
                    var packageSourcesSection = SimpleTestSettingsContext.GetOrAddSection(settings.XML, "packageSources");
                    SimpleTestSettingsContext.AddEntry(packageSourcesSection, "vsts", source, additionalAtrributeName: "protocolVersion", additionalAttributeValue: "2", additionalAtrributeName2: "allowInsecureConnections", additionalAttributeValue2: "true");

                    SimpleTestSettingsContext.AddPackageSourceCredentialsSection(settings.XML, "vsts", "user", "password", clearTextPassword: true);
                    settings.Save();

                    serverV3.Start();

                    // Act
                    var result = CommandRunner.Run(
                        Util.GetNuGetExePath(),
                        Directory.GetCurrentDirectory(),
                        $"list test -source {serverV3.Uri}api/v2 -configfile {pathContext.NuGetConfig} -verbosity detailed -noninteractive");
                    serverV3.Stop();

                    // Assert
                    Assert.True(0 == result.ExitCode, $"{result.Output} {result.Errors}");
                    Assert.True(serverReceiveProperAuthorizationHeader);
                    Assert.Contains($"GET {serverV3.Uri}{listEndpoint}/Search()", result.Output);
                    // verify that only package id & version is displayed
                    Assert.Matches(@"(?m)testPackage1\s+1\.1\.0", result.Output);

                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("true", false)]
        [InlineData("false", true)]
        public void ListCommand_WhenListWithHttpSourceAndAllowInsecureConnections_ProducesAnErrorCorrectly(string allowInsecureConnections, bool isHttpErrorExpected)
        {
            var nugetexe = Util.GetNuGetExePath();

            // Arrange
            using var pathContext = new SimpleTestPathContext();
            using var server = new MockServer();
            var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", pathContext.WorkingDirectory);

            server.Get.Add("/nuget/$metadata", r =>
                Util.GetMockServerResource());
            server.Get.Add("/nuget/Search()", r =>
                new Action<HttpListenerResponse>(response =>
                {
                    string searchRequest = r.Url.ToString();
                    response.ContentType = "application/atom+xml;type=feed;charset=utf-8";
                    string feed = server.ToODataFeed(new[] { new FileInfo(packageFileName1) }, "Search");
                    MockServer.SetResponseContent(response, feed);
                }));
            server.Get.Add("/nuget", r => "OK");

            server.Start();

            // create the config file
            pathContext.Settings.AddSource("http-feed", $"{server.Uri}nuget", allowInsecureConnectionsValue: allowInsecureConnections);

            var configFile = Path.Combine(pathContext.WorkingDirectory, "nuget.config");
            PackageSource source = new PackageSource(server.Uri + "nuget", "http-feed");
            string expectedError = string.Format(CultureInfo.CurrentCulture, _httpErrorSingle, "list", source);

            // Act
            var args = "list test -ConfigFile " + configFile;
            var result = CommandRunner.Run(
                nugetexe,
                pathContext.WorkingDirectory,
                args);
            server.Stop();

            // Assert
            if (isHttpErrorExpected)
            {
                Assert.False(result.Success);
                Assert.Contains(expectedError, result.AllOutput);
            }
            else
            {
                Assert.Equal(0, result.ExitCode);
                Assert.DoesNotContain(expectedError, result.AllOutput);
                // verify that only package id & version is displayed
                var expectedOutput = "testPackage1 1.1.0";
                Assert.Contains(expectedOutput, result.Output);
            }
        }

        [Fact]
        public void ListCommand_WhenListWithHttpSources_DisplaysAnError()
        {
            var nugetexe = Util.GetNuGetExePath();

            // Arrange
            using var packageDirectory = TestDirectory.Create();
            using var server1 = new MockServer();
            using var server2 = new MockServer();
            var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);

            server1.Get.Add("/nuget/$metadata", r =>
                Util.GetMockServerResource());
            server1.Get.Add("/nuget/Search()", r =>
                new Action<HttpListenerResponse>(response =>
                {
                    string searchRequest = r.Url.ToString();
                    response.ContentType = "application/atom+xml;type=feed;charset=utf-8";
                    string feed = server1.ToODataFeed(new[] { new FileInfo(packageFileName1) }, "Search");
                    MockServer.SetResponseContent(response, feed);
                }));
            server1.Get.Add("/nuget", r => "OK");

            server1.Start();

            server2.Get.Add("/nuget/$metadata", r =>
               Util.GetMockServerResource());
            server2.Get.Add("/nuget/Search()", r =>
                new Action<HttpListenerResponse>(response =>
                {
                    string searchRequest = r.Url.ToString();
                    response.ContentType = "application/atom+xml;type=feed;charset=utf-8";
                    string feed = server2.ToODataFeed(new[] { new FileInfo(packageFileName1) }, "Search");
                    MockServer.SetResponseContent(response, feed);
                }));
            server2.Get.Add("/nuget", r => "OK");

            server2.Start();

            PackageSource source1 = new PackageSource(server1.Uri + "nuget", "http-feed1");
            PackageSource source2 = new PackageSource(server2.Uri + "nuget", "http-feed2");
            List<PackageSource> sources = new List<PackageSource>() { source1, source2 };
            string expectedError = string.Format(CultureInfo.CurrentCulture,
                        _httpErrorMultiple,
                        "list",
                        Environment.NewLine + string.Join(Environment.NewLine, sources.Select(e => e.SourceUri)));

            // Act
            var args = "list test -Source " + server1.Uri + "nuget" + " -Source " + server2.Uri + "nuget";
            var result = CommandRunner.Run(
                nugetexe,
                packageDirectory,
                args);
            server1.Stop();
            server2.Stop();

            // Assert
            Assert.Equal(1, result.ExitCode);

            Assert.Contains(expectedError, result.AllOutput);
        }

        [Fact]
        public void ListCommand_CheckDeprecationMessage()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();
            string deprecation = string.Format(NuGetResources.Warning_CommandDeprecated, "NuGet", "list", "search");

            using (var repositoryPath = TestDirectory.Create())
            {
                Util.CreateTestPackage("testPackage1", "1.1.0", repositoryPath);
                Util.CreateTestPackage("testPackage2", "2.0.0", repositoryPath);

                string[] args = ["list", "-Source", repositoryPath];

                // Act
                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args));

                // Assert
                Assert.Equal(0, result.ExitCode);
                Assert.Equal($"WARNING: {deprecation}{Environment.NewLine}testPackage1 1.1.0{Environment.NewLine}testPackage2 2.0.0{Environment.NewLine}", result.Output);
            }
        }

        private static string RemoveDeprecationWarning(string input)
        {
            string[] lines = input.Split(
                new string[] { "\r\n", "\r", "\n" },
                StringSplitOptions.None
            );

            return string.Join(Environment.NewLine, lines.Select(e => e).Where(e => !e.StartsWith("WARNING: 'NuGet list' is deprecated.")));
        }
    }
}
