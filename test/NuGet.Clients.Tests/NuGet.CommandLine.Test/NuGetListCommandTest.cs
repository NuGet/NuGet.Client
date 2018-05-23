// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using NuGet.Test.Utility;
using Xunit;
using System.Text;
using NuGet.Common;

namespace NuGet.CommandLine.Test
{
    public class NuGetListCommandTest
    {
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
                waitForExit: true,
                environmentVariables: new Dictionary<string, string>
                {
                    { "NUGET_SHOW_STACK", "true" }
                });

            // Assert
            Assert.Contains(expected, result.Item2 + " " + result.Item3);
            Assert.NotEqual(0, result.Item1);
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
                expected = "NameResolutionFailure";
            }
            var args = new[] { "list", "-Source", "https://" + hostName + "/" };

            // Act
            var result = CommandRunner.Run(
                nugetexe,
                Directory.GetCurrentDirectory(),
                string.Join(" ", args),
                waitForExit: true,
                environmentVariables: new Dictionary<string, string>
                {
                    { "NUGET_SHOW_STACK", "false" }
                });

            // Assert
            Assert.Contains(expected, result.Item2 + " " + result.Item3);
            Assert.NotEqual(0, result.Item1);
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
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(0, result.Item1);
                var output = Util.TrimMSBuildDiscoveryAutoDetectionMessage(result.Item2);
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
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                var output = r.Item2;
                string[] lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                Assert.Equal(6, lines.Length);
                Assert.Equal("testPackage1", lines[2]);
                Assert.Equal(" 1.1.0", lines[3]);
                Assert.Equal(" desc of testPackage1 1.1.0", lines[4]);
                Assert.Equal(" License url: http://kaka", lines[5]);
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
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(0, result.Item1);
                var output = Util.TrimMSBuildDiscoveryAutoDetectionMessage(result.Item2);
                Assert.Equal($"testPackage1 1.1.0{Environment.NewLine}testPackage2 2.0.0{Environment.NewLine}", output);
            }
        }

        // Tests list command, with no other switches
        [Fact]
        public void ListCommand_Simple()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            using (var randomTestFolder = TestDirectory.Create())
            {
                // Arrange
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new ZipPackage(packageFileName1);
                var package2 = new ZipPackage(packageFileName2);

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

                    // Act
                    var args = "list test -Source " + server.Uri + "nuget";
                    var result = CommandRunner.Run(
                        nugetexe,
                        randomTestFolder,
                        args,
                        waitForExit: true);
                    server.Stop();

                    // Assert
                    Assert.Equal(0, result.Item1);

                    // verify that only package id & version is displayed
                    var expectedOutput = "testPackage1 1.1.0" + Environment.NewLine +
                        "testPackage2 2.1.0" + Environment.NewLine;
                    Assert.Equal(expectedOutput, Util.TrimMSBuildDiscoveryAutoDetectionMessage(result.Item2));

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
            using (var randomTestFolder = TestDirectory.Create())
            {
                // Arrange
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new ZipPackage(packageFileName1);
                var package2 = new ZipPackage(packageFileName2);
                package1.Published = new DateTimeOffset?(new DateTime(1800, 1, 1));
                package1.Listed = false;

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

                    // Act
                    var args = "list test -Source " + server.Uri + "nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        randomTestFolder,
                        args,
                        waitForExit: true);
                    server.Stop();

                    // Assert
                    Assert.True(r1.Item1 == 0, r1.Item2 + " " + r1.Item3);

                    // verify that only testPackage2 is listed since the package testPackage1
                    // is not listed.
                    var expectedOutput = "testPackage2 2.1.0" + Environment.NewLine;
                    Assert.Equal(expectedOutput, Util.TrimMSBuildDiscoveryAutoDetectionMessage(r1.Item2));

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
            using (var randomTestFolder = TestDirectory.Create())
            {
                // Arrange
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new ZipPackage(packageFileName1);
                var package2 = new ZipPackage(packageFileName2);
                package1.Listed = false;

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

                    // Act
                    var args = "list test -IncludeDelisted -Source " + server.Uri + "nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        randomTestFolder,
                        args,
                        waitForExit: true);
                    server.Stop();

                    // Assert
                    Assert.True(r1.Item1 == 0, r1.Item2 + " " + r1.Item3);

                    // verify that both testPackage1 and testPackage2 are listed.
                    var expectedOutput =
                        "testPackage1 1.1.0" + Environment.NewLine +
                        "testPackage2 2.1.0" + Environment.NewLine;
                    Assert.Equal(expectedOutput, Util.TrimMSBuildDiscoveryAutoDetectionMessage(r1.Item2));

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
            using (var randomTestFolder = TestDirectory.Create())
            {
                // Arrange
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new ZipPackage(packageFileName1);
                var package2 = new ZipPackage(packageFileName2);

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

                    // Act
                    var args = "list test -Verbosity detailed -Source " + server.Uri + "nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        randomTestFolder,
                        args,
                        waitForExit: true);
                    server.Stop();

                    // Assert
                    Assert.Equal(0, r1.Item1);

                    // verify that the output is detailed
                    Assert.Contains(package1.Description, r1.Item2);
                    Assert.Contains(package2.Description, r1.Item2);

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
            using (var randomTestFolder = TestDirectory.Create())
            {
                // Arrange
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new ZipPackage(packageFileName1);
                var package2 = new ZipPackage(packageFileName2);

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

                    // Act
                    var args = "list test -AllVersions -Source " + server.Uri + "nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        randomTestFolder,
                        args,
                        waitForExit: true);
                    server.Stop();

                    // Assert
                    Assert.Equal(0, r1.Item1);

                    // verify that the output is detailed
                    var expectedOutput = "testPackage1 1.1.0" + Environment.NewLine +
                        "testPackage2 2.1.0" + Environment.NewLine;
                    Assert.Equal(expectedOutput, Util.TrimMSBuildDiscoveryAutoDetectionMessage(r1.Item2));

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
            Util.ClearWebCache();
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            using (var randomTestFolder = TestDirectory.Create())
            {
                // Arrange
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new ZipPackage(packageFileName1);
                var package2 = new ZipPackage(packageFileName2);

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

                    // Act
                    var args = "list test -Prerelease -Source " + server.Uri + "nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        randomTestFolder,
                        args,
                        waitForExit: true);
                    server.Stop();

                    // Assert
                    Assert.Equal(0, r1.Item1);

                    // verify that the output is detailed
                    var expectedOutput = "testPackage1 1.1.0" + Environment.NewLine +
                        "testPackage2 2.1.0" + Environment.NewLine;
                    Assert.Equal(expectedOutput, Util.TrimMSBuildDiscoveryAutoDetectionMessage(r1.Item2));

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
            Util.ClearWebCache();
            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            using (var randomTestFolder = TestDirectory.Create())
            {
                // Arrange
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new ZipPackage(packageFileName1);
                var package2 = new ZipPackage(packageFileName2);

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

                    // Act
                    var args = "list test -AllVersions -Prerelease -Source " + server.Uri + "nuget";
                    var r1 = CommandRunner.Run(
                        nugetexe,
                        randomTestFolder,
                        args,
                        waitForExit: true);
                    server.Stop();

                    // Assert
                    Assert.Equal(0, r1.Item1);

                    // verify that the output is detailed
                    var expectedOutput = "testPackage1 1.1.0" + Environment.NewLine +
                        "testPackage2 2.1.0" + Environment.NewLine;
                    Assert.Equal(expectedOutput, Util.TrimMSBuildDiscoveryAutoDetectionMessage(r1.Item2));

                    Assert.DoesNotContain("$filter", searchRequest);
                    Assert.Contains("searchTerm='test", searchRequest);
                    Assert.Contains("includePrerelease=true", searchRequest);
                }
            }
        }

        [Fact]
        public void ListCommand_SimpleV3()
        {
            Util.ClearWebCache();

            var nugetexe = Util.GetNuGetExePath();

            using (var packageDirectory = TestDirectory.Create())
            {
                // Arrange
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new ZipPackage(packageFileName1);
                var package2 = new ZipPackage(packageFileName2);

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

                        // Act
                        var args = "list test -Source " + serverV3.Uri + "index.json";
                        var result = CommandRunner.Run(
                            nugetexe,
                            Directory.GetCurrentDirectory(),
                            args,
                            waitForExit: true);

                        serverV2.Stop();
                        serverV3.Stop();

                        // Assert
                        Assert.True(result.Item1 == 0, result.Item2 + " " + result.Item3);

                        // verify that only package id & version is displayed
                        var expectedOutput = "testPackage1 1.1.0" + Environment.NewLine +
                            "testPackage2 2.1.0" + Environment.NewLine;
                        Assert.Equal(expectedOutput, Util.TrimMSBuildDiscoveryAutoDetectionMessage(result.Item2));

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
            Util.ClearWebCache();
            var nugetexe = Util.GetNuGetExePath();
            using (var packageDirectory = TestDirectory.Create())
            {
                // Arrange
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

                    serverV3.Start();

                    // Act
                    var args = "list test -Source " + serverV3.Uri + "index.json";
                    var result = CommandRunner.Run(
                        nugetexe,
                        Directory.GetCurrentDirectory(),
                        args,
                        waitForExit: true);

                    serverV3.Stop();

                    // Assert
                    Assert.True(result.Item1 == 0, result.Item2 + " " + result.Item3);

                    // verify that only package id & version is displayed
                    var expectedOutput =
                        string.Format(
                      "WARNING: This version of nuget.exe does not support listing packages" +
                      " from package source '{0}'.",
                      serverV3.Uri + "index.json");

                    // Verify that the output contains the expected output
                    Assert.True(result.Item2.Contains(expectedOutput));
                }
            }
        }

        [Fact]
        public void ListCommand_UnavailableV3()
        {
            Util.ClearWebCache();

            var nugetexe = Util.GetNuGetExePath();
            using (var packageDirectory = TestDirectory.Create())

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

                    // Act
                    var args = "list test -Source " + serverV3.Uri + "index.json";
                    var result = CommandRunner.Run(
                        nugetexe,
                        Directory.GetCurrentDirectory(),
                        args,
                        waitForExit: true);

                    serverV3.Stop();

                    // Assert
                    Assert.True(result.Item1 != 0, result.Item2 + " " + result.Item3);

                    Assert.True(
                        result.Item3.Contains("404 (Not Found)"),
                        "Expected error message not found in " + result.Item3
                        );
                }
            }
        }

        [Theory]
        [InlineData("invalid")]
        public void ListCommand_InvalidInput_NonSource(string invalidInput)
        {
            Util.ClearWebCache();

            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            // Act
            var args = "list test -Source " + invalidInput;
            var result = CommandRunner.Run(
                nugetexe,
                Directory.GetCurrentDirectory(),
                args,
                waitForExit: true);

            // Assert
            Assert.True(
                result.Item1 != 0,
                "The run did not fail as desired. Simply got this output:" + result.Item2);

            Assert.True(
                result.Item3.Contains(
                    string.Format(
                        "The specified source '{0}' is invalid. Please provide a valid source.",
                        invalidInput)),
                "Expected error message not found in " + result.Item3
                );
        }

        [Theory]
        [InlineData("https://invalid-2a0358f1-88f2-48c0-b68a-bb150cac00bd.org")]
        [InlineData("https://invalid-2a0358f1-88f2-48c0-b68a-bb150cac00bd.org/api/v2")]
        public void ListCommand_InvalidInput_V2_NonExistent(string invalidInput)
        {
            // Arrange
            Util.ClearWebCache();
            var nugetexe = Util.GetNuGetExePath();

            // Act
            var args = "list test -Source " + invalidInput;
            var result = CommandRunner.Run(
                nugetexe,
                Directory.GetCurrentDirectory(),
                args,
                waitForExit: true);

            // Assert
            Assert.True(
                result.Item1 != 0,
                "The run did not fail as desired. Simply got this output:" + result.Item2);

            if (RuntimeEnvironmentHelper.IsMono)
            {
                Assert.True(
               result.Item3.Contains(
                   "NameResolutionFailure"),
               "Expected error message not found in " + result.Item3
               );
            }
            else
            {
                Assert.True(
                    result.Item3.Contains(
                        $"Unable to load the service index for source {invalidInput}."),
                    "Expected error message not found in " + result.Item3
                    );
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
                args,
                waitForExit: true);

            // Assert
            Assert.True(
                result.Item1 != 0,
                "The run did not fail as desired. Simply got this output:" + result.Item2);

            Assert.True(
                result.Item3.Contains(
                    "returned an unexpected status code '404 Not Found'."),
                "Expected error message not found in:\n " + result.Item3
                );
        }

        [Theory]
        [InlineData("https://invalid-2a0358f1-88f2-48c0-b68a-bb150cac00bd.org/v3/index.json")]
        public void ListCommand_InvalidInput_V3_NonExistent(string invalidInput)
        {
            // Arrange
            Util.ClearWebCache();
            var nugetexe = Util.GetNuGetExePath();

            // Act
            var args = "list test -Source " + invalidInput;
            var result = CommandRunner.Run(
                nugetexe,
                Directory.GetCurrentDirectory(),
                args,
                waitForExit: true);

            // Assert
            Assert.True(
                result.Item1 != 0,
                "The run did not fail as desired. Simply got this output:" + result.Item2);

            Assert.True(
                result.Item3.Contains($"Unable to load the service index for source {invalidInput}."),
                "Expected error message not found in " + result.Item3
                );
        }

        [Theory]
        [InlineData("https://api.nuget.org/v4/index.json")]
        public void ListCommand_InvalidInput_V3_NotFound(string invalidInput)
        {
            // Arrange
            Util.ClearWebCache();
            var nugetexe = Util.GetNuGetExePath();

            // Act
            var args = "list test -Source " + invalidInput;
            var result = CommandRunner.Run(
                nugetexe,
                Directory.GetCurrentDirectory(),
                args,
                waitForExit: true);

            // Assert
            Assert.True(
                result.Item1 != 0,
                "The run did not fail as desired. Simply got this output:" + result.Item2);

            Assert.True(
                result.Item3.Contains("400 (Bad Request)"),
                "Expected error message not found in " + result.Item3
                );
        }

        [Fact]
        public void ListCommand_WithAuthenticatedSource_AppliesCredentialsFromSettings()
        {
            Util.ClearWebCache();
            var expectedAuthHeader = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user:password"));
            var listEndpoint = Guid.NewGuid().ToString() + "/api/v2";

            using (var randomTestFolder = TestDirectory.Create())
            {
                // Arrange
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", randomTestFolder);
                var package1 = new ZipPackage(packageFileName1);

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

                        return "OK";
                    });

                    var config = $@"<?xml version='1.0' encoding='utf-8'?>
                                <configuration>
                                  <packageSources>
                                    <add key='vsts' value='{serverV3.Uri}index.json' protocolVersion='3' />
                                  </packageSources>
                                  <packageSourceCredentials>
                                    <vsts>
                                      <add key='Username' value='user' />
                                      <add key='ClearTextPassword' value='password' />
                                    </vsts>
                                  </packageSourceCredentials>
                                 </configuration>";
                    var configFileName = Path.Combine(randomTestFolder, "nuget.config");
                    File.WriteAllText(configFileName, config);

                    serverV3.Start();

                    // Act
                    var result = CommandRunner.Run(
                        Util.GetNuGetExePath(),
                        Directory.GetCurrentDirectory(),
                        $"list test -source {serverV3.Uri}index.json -configfile {configFileName} -verbosity detailed -noninteractive",
                        waitForExit: true);

                    serverV3.Stop();
                    // Assert
                    Assert.True(0 == result.Item1, $"{result.Item2} {result.Item3}");
                    Assert.Contains("Using credentials from config. UserName: user", result.Item2);
                    Assert.Contains($"GET {serverV3.Uri}{listEndpoint}/Search()", result.Item2);
                    // verify that only package id & version is displayed
                    Assert.Matches(@"(?m)testPackage1\s+1\.1\.0", result.Item2);

                }
            }
        }
        [Fact]
        public void ListCommand_WithAuthenticatedSourceV2_AppliesCredentialsFromSettings()
        {
            Util.ClearWebCache();
            var expectedAuthHeader = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user:password"));
            var listEndpoint = "api/v2";

            using (var randomTestFolder = TestDirectory.Create())
            {
                // Arrange
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", randomTestFolder);
                var package1 = new ZipPackage(packageFileName1);

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

                        return "OK";
                    });

                    var config = $@"<?xml version='1.0' encoding='utf-8'?>
                    <configuration>
                      <packageSources>
                        <add key='vsts' value='{serverV3.Uri}api/v2' protocolVersion='2' />
                      </packageSources>
                      <packageSourceCredentials>
                        <vsts>
                          <add key='Username' value='user' />
                          <add key='ClearTextPassword' value='password' />
                        </vsts>
                      </packageSourceCredentials>
                     </configuration>";
                    var configFileName = Path.Combine(randomTestFolder, "nuget.config");
                    File.WriteAllText(configFileName, config);

                    serverV3.Start();

                    // Act
                    var result = CommandRunner.Run(
                        Util.GetNuGetExePath(),
                        Directory.GetCurrentDirectory(),
                        $"list test -source {serverV3.Uri}api/v2 -configfile {configFileName} -verbosity detailed -noninteractive",
                        waitForExit: true);

                    serverV3.Stop();

                    // Assert
                    Assert.True(0 == result.Item1, $"{result.Item2} {result.Item3}");
                    Assert.Contains("Using credentials from config. UserName: user", result.Item2);
                    Assert.Contains($"GET {serverV3.Uri}{listEndpoint}/Search()", result.Item2);
                    // verify that only package id & version is displayed
                    Assert.Matches(@"(?m)testPackage1\s+1\.1\.0", result.Item2);

                }
            }
        }
    }
}
