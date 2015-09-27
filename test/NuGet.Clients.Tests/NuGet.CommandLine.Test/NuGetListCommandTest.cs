using System;
using System.IO;
using System.Net;
using System.Text;
using Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetListCommandTest
    {
        [Fact]
        public void ListCommand_WithUserSpecifiedSource()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var repositoryPath = Path.Combine(tempPath, Guid.NewGuid().ToString());
            Util.CreateDirectory(repositoryPath);
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
            var output = result.Item2;
            Assert.Equal("testPackage1 1.1.0\r\ntestPackage2 2.0.0\r\n", output);
        }

        [Fact]
        public void ListCommand_ShowLicenseUrlWithDetailedVerbosity()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var repositoryPath = Path.Combine(tempPath, Guid.NewGuid().ToString());
            Util.CreateDirectory(repositoryPath);
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

            Assert.Equal(4, lines.Length);
            Assert.Equal("testPackage1", lines[0]);
            Assert.Equal(" 1.1.0", lines[1]);
            Assert.Equal(" desc of testPackage1 1.1.0", lines[2]);
            Assert.Equal(" License url: http://kaka", lines[3]);
        }

        [Fact]
        public void ListCommand_WithUserSpecifiedConfigFile()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var repositoryPath = Path.Combine(tempPath, Guid.NewGuid().ToString());
            Util.CreateDirectory(repositoryPath);
            Util.CreateTestPackage("testPackage1", "1.1.0", repositoryPath);
            Util.CreateTestPackage("testPackage2", "2.0.0", repositoryPath);

            // create the config file
            var configFile = Path.GetTempFileName();
            Util.CreateFile(Path.GetDirectoryName(configFile), Path.GetFileName(configFile), "<configuration/>");

            string[] args = new string[] {
                "sources",
                "Add",
                "-Name",
                "test_source",
                "-Source",
                repositoryPath,
                "-ConfigFile",
                configFile
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
            File.Delete(configFile);

            // Assert
            Assert.Equal(0, result.Item1);
            var output = result.Item2;
            Assert.Equal("testPackage1 1.1.0\r\ntestPackage2 2.0.0\r\n", output);
        }

        // Tests list command, with no other switches
        [Fact]
        public void ListCommand_Simple()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new ZipPackage(packageFileName1);
                var package2 = new ZipPackage(packageFileName2);

                using (var server = new MockServer())
                {
                    string searchRequest = string.Empty;

                    server.Get.Add("/nuget/$metadata", r =>
                        MockServerResource.NuGetV2APIMetadata);
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
                        tempPath,
                        args,
                        waitForExit: true);
                    server.Stop();

                    // Assert
                    Assert.Equal(0, result.Item1);

                    // verify that only package id & version is displayed
                    var expectedOutput = "testPackage1 1.1.0" + Environment.NewLine +
                        "testPackage2 2.1" + Environment.NewLine;
                    Assert.Equal(expectedOutput, result.Item2);

                    Assert.Contains("$filter=IsLatestVersion", searchRequest);
                    Assert.Contains("searchTerm='test", searchRequest);
                    Assert.Contains("includePrerelease=false", searchRequest);
                }
            }
            finally
            {
                // Cleanup
                Util.DeleteDirectory(packageDirectory);
            }
        }

        // Tests that list command only show listed packages
        [Fact]
        public void ListCommand_OnlyShowListed()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new ZipPackage(packageFileName1);
                var package2 = new ZipPackage(packageFileName2);
                package1.Listed = false;

                using (var server = new MockServer())
                {
                    string searchRequest = string.Empty;

                    server.Get.Add("/nuget/$metadata", r =>
                        MockServerResource.NuGetV2APIMetadata);
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
                        tempPath,
                        args,
                        waitForExit: true);
                    server.Stop();

                    // Assert
                    Assert.True(r1.Item1 == 0, r1.Item2 + " " + r1.Item3);

                    // verify that only testPackage2 is listed since the package testPackage1
                    // is not listed.
                    var expectedOutput = "testPackage2 2.1" + Environment.NewLine;
                    Assert.Equal(expectedOutput, r1.Item2);

                    Assert.Contains("$filter=IsLatestVersion", searchRequest);
                    Assert.Contains("searchTerm='test", searchRequest);
                    Assert.Contains("includePrerelease=false", searchRequest);

                    // verify that nuget doesn't include "includeDelisted" in its request
                    Assert.DoesNotContain("includeDelisted", searchRequest);
                }
            }
            finally
            {
                // Cleanup
                Util.DeleteDirectory(packageDirectory);
            }
        }

        // Tests that list command show delisted packages 
        // when IncludeDelisted is specified.
        [Fact]
        public void ListCommand_IncludeDelisted()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new ZipPackage(packageFileName1);
                var package2 = new ZipPackage(packageFileName2);
                package1.Listed = false;

                using (var server = new MockServer())
                {
                    string searchRequest = string.Empty;

                    server.Get.Add("/nuget/$metadata", r =>
                        MockServerResource.NuGetV2APIMetadata);
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
                        tempPath,
                        args,
                        waitForExit: true);
                    server.Stop();

                    // Assert
                    Assert.True(r1.Item1 == 0, r1.Item2 + " " + r1.Item3);

                    // verify that both testPackage1 and testPackage2 are listed.
                    var expectedOutput =
                        "testPackage1 1.1.0" + Environment.NewLine +
                        "testPackage2 2.1" + Environment.NewLine;
                    Assert.Equal(expectedOutput, r1.Item2);

                    Assert.Contains("$filter=IsLatestVersion", searchRequest);
                    Assert.Contains("searchTerm='test", searchRequest);
                    Assert.Contains("includePrerelease=false", searchRequest);
                }
            }
            finally
            {
                // Cleanup
                Util.DeleteDirectory(packageDirectory);
            }
        }

        // Tests that list command displays detailed package info when -Verbosity is detailed.
        [Fact]
        public void ListCommand_VerboseOutput()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new ZipPackage(packageFileName1);
                var package2 = new ZipPackage(packageFileName2);

                using (var server = new MockServer())
                {
                    string searchRequest = string.Empty;

                    server.Get.Add("/nuget/$metadata", r =>
                        MockServerResource.NuGetV2APIMetadata);
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
                        tempPath,
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
            finally
            {
                // Cleanup
                Util.DeleteDirectory(packageDirectory);
            }
        }

        // Tests that when -AllVersions is specified, list command sends request 
        // without $filter
        [Fact]
        public void ListCommand_AllVersions()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new ZipPackage(packageFileName1);
                var package2 = new ZipPackage(packageFileName2);

                using (var server = new MockServer())
                {
                    string searchRequest = string.Empty;

                    server.Get.Add("/nuget/$metadata", r =>
                        MockServerResource.NuGetV2APIMetadata);
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
                        tempPath,
                        args,
                        waitForExit: true);
                    server.Stop();

                    // Assert
                    Assert.Equal(0, r1.Item1);

                    // verify that the output is detailed
                    var expectedOutput = "testPackage1 1.1.0" + Environment.NewLine +
                        "testPackage2 2.1" + Environment.NewLine;
                    Assert.Equal(expectedOutput, r1.Item2);

                    Assert.DoesNotContain("$filter", searchRequest);
                    Assert.Contains("searchTerm='test", searchRequest);
                    Assert.Contains("includePrerelease=false", searchRequest);
                }
            }
            finally
            {
                // Cleanup
                Util.DeleteDirectory(packageDirectory);
            }
        }

        // Test case when switch -Prerelease is specified
        [Fact]
        public void ListCommand_Prerelease()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new ZipPackage(packageFileName1);
                var package2 = new ZipPackage(packageFileName2);

                using (var server = new MockServer())
                {
                    string searchRequest = string.Empty;

                    server.Get.Add("/nuget/$metadata", r =>
                        MockServerResource.NuGetV2APIMetadata);
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
                        tempPath,
                        args,
                        waitForExit: true);
                    server.Stop();

                    // Assert
                    Assert.Equal(0, r1.Item1);

                    // verify that the output is detailed
                    var expectedOutput = "testPackage1 1.1.0" + Environment.NewLine +
                        "testPackage2 2.1" + Environment.NewLine;
                    Assert.Equal(expectedOutput, r1.Item2);

                    Assert.Contains("$filter=IsAbsoluteLatestVersion", searchRequest);
                    Assert.Contains("searchTerm='test", searchRequest);
                    Assert.Contains("includePrerelease=true", searchRequest);
                }
            }
            finally
            {
                // Cleanup
                Util.DeleteDirectory(packageDirectory);
            }
        }

        // Test case when both switches -Prerelease and -AllVersions are specified
        [Fact]
        public void ListCommand_AllVersionsPrerelease()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new ZipPackage(packageFileName1);
                var package2 = new ZipPackage(packageFileName2);

                using (var server = new MockServer())
                {
                    string searchRequest = string.Empty;

                    server.Get.Add("/nuget/$metadata", r =>
                        MockServerResource.NuGetV2APIMetadata);
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
                        tempPath,
                        args,
                        waitForExit: true);
                    server.Stop();

                    // Assert
                    Assert.Equal(0, r1.Item1);

                    // verify that the output is detailed
                    var expectedOutput = "testPackage1 1.1.0" + Environment.NewLine +
                        "testPackage2 2.1" + Environment.NewLine;
                    Assert.Equal(expectedOutput, r1.Item2);

                    Assert.DoesNotContain("$filter", searchRequest);
                    Assert.Contains("searchTerm='test", searchRequest);
                    Assert.Contains("includePrerelease=true", searchRequest);
                }
            }
            finally
            {
                // Cleanup
                Util.DeleteDirectory(packageDirectory);
            }
        }

        [Fact]
        public void ListCommand_SimpleV3()
        {
            var nugetexe = Util.GetNuGetExePath();
            var packageDirectory = TestFilesystemUtility.CreateRandomTestFolder();

            try
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
                        var path = r.Url.AbsolutePath;

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
                        Util.AddLegacyUrlResource(indexJson, serverV2);
                        string searchRequest = string.Empty;

                        serverV2.Get.Add("/", r =>
                        {
                            var path = r.Url.AbsolutePath;

                            if (path == "/")
                            {
                                return "OK";
                            }

                            if (path == "/$metadata")
                            {
                                return MockServerResource.NuGetV2APIMetadata;
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
                            "testPackage2 2.1" + Environment.NewLine;
                        Assert.Equal(expectedOutput, result.Item2);

                        Assert.Contains("$filter=IsLatestVersion", searchRequest);
                        Assert.Contains("searchTerm='test", searchRequest);
                        Assert.Contains("includePrerelease=false", searchRequest);
                    }
                }
            }
            finally
            {
                // Cleanup
                TestFilesystemUtility.DeleteRandomTestFolders(packageDirectory);
            }
        }

        [Fact]
        public void ListCommand_SimpleV3_NoListEndpoint()
        {
            var nugetexe = Util.GetNuGetExePath();
            var packageDirectory = TestFilesystemUtility.CreateRandomTestFolder();

            try
            {
                // Arrange
                // Server setup
                var indexJson = Util.CreateIndexJson();
                using (var serverV3 = new MockServer())
                {
                    serverV3.Get.Add("/", r =>
                    {
                        var path = r.Url.AbsolutePath;

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
            finally
            {
                // Cleanup
                TestFilesystemUtility.DeleteRandomTestFolders(packageDirectory);
            }
        }

        [Fact]
        public void ListCommand_UnavailableV3()
        {
            var nugetexe = Util.GetNuGetExePath();
            var packageDirectory = TestFilesystemUtility.CreateRandomTestFolder();

            try
            {
                // Arrange
                // Server setup
                using (var serverV3 = new MockServer())
                {
                    serverV3.Get.Add("/", r =>
                    {
                        var path = r.Url.AbsolutePath;

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
                        result.Item3.Contains("Response status code does not indicate success: 404 (Not Found)."),
                        "Expected error message not found in " + result.Item3
                        );
                }
            }
            finally
            {
                // Cleanup
                TestFilesystemUtility.DeleteRandomTestFolders(packageDirectory);
            }
        }

        [Theory]
        [InlineData("invalid")]
        public void ListCommand_InvalidInput_NonSource(string invalidInput)
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
                    "The remote name could not be resolved: 'invalid-2a0358f1-88f2-48c0-b68a-bb150cac00bd.org'"),
                "Expected error message not found in " + result.Item3
                );
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
                    "The remote server returned an error: (404) Not Found."),
                "Expected error message not found in " + result.Item3
                );
        }

        [Theory]
        [InlineData("https://invalid-2a0358f1-88f2-48c0-b68a-bb150cac00bd.org/v3/index.json")]
        public void ListCommand_InvalidInput_V3_NonExistent(string invalidInput)
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
                result.Item3.Contains("An error occurred while sending the request."),
                "Expected error message not found in " + result.Item3
                );
        }

        [Theory]
        [InlineData("https://api.nuget.org/v4/index.json")]
        public void ListCommand_InvalidInput_V3_NotFound(string invalidInput)
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
                result.Item3.Contains("Response status code does not indicate success: 400 (Bad Request)."),
                "Expected error message not found in " + result.Item3
                );
        }
    }
}
