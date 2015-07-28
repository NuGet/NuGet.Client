using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Xunit;
using Xunit.Extensions;

namespace NuGet.CommandLine.Test
{
    public class ListCommandTest
    {
        [Fact]
        public void ListCommand_WithUserSpecifiedSource()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var repositoryPath = Path.Combine(tempPath, Guid.NewGuid().ToString());
            Util.CreateDirectory(repositoryPath);
            Util.CreateTestPackage("testPackage1", "1.1.0", repositoryPath);
            Util.CreateTestPackage("testPackage2", "2.0.0", repositoryPath);

            string[] args = new string[] { "list", "-Source", repositoryPath };
            MemoryStream memoryStream = new MemoryStream();
            TextWriter writer = new StreamWriter(memoryStream);
            Console.SetOut(writer);

            // Act
            int r = Program.Main(args);
            writer.Close();

            // Assert
            Assert.Equal(0, r);
            var output = Encoding.Default.GetString(memoryStream.ToArray());
            Assert.Equal("testPackage1 1.1.0\r\ntestPackage2 2.0.0\r\n", output);
        }

        [Fact(Skip = "nuget.exe list does not return license url. This is a tracked issue")]
        public void ListCommand_ShowLicenseUrlWithDetailedVerbosity()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            var repositoryPath = Path.Combine(tempPath, Guid.NewGuid().ToString());
            Util.CreateDirectory(repositoryPath);
            Util.CreateTestPackage("testPackage1", "1.1.0", repositoryPath, new Uri("http://kaka"));

            string[] args = new string[] { "list", "-Source", repositoryPath, "-verbosity", "detailed" };
            MemoryStream memoryStream = new MemoryStream();
            TextWriter writer = new StreamWriter(memoryStream);
            Console.SetOut(writer);

            // Act
            int r = Program.Main(args);
            writer.Close();

            // Assert
            Assert.Equal(0, r);
            var output = Encoding.Default.GetString(memoryStream.ToArray());
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
            MemoryStream memoryStream = new MemoryStream();
            TextWriter writer = new StreamWriter(memoryStream);
            Console.SetOut(writer);

            r = Program.Main(args);
            writer.Close();
            File.Delete(configFile);           

            // Assert
            Assert.Equal(0, r);
            var output = Encoding.Default.GetString(memoryStream.ToArray());
            Assert.Equal("testPackage1 1.1.0\r\ntestPackage2 2.0.0\r\n", output);
        }

        // Tests list command, with no other switches
        [Fact]
        public void ListCommand_Simple()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var mockServerEndPoint = "http://localhost:1234/";

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new ZipPackage(packageFileName1);
                var package2 = new ZipPackage(packageFileName2);
                
                var server = new MockServer(mockServerEndPoint);
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
                var args = "list test -Source " + mockServerEndPoint + "nuget";
                var r1 = CommandRunner.Run(
                    nugetexe,
                    tempPath,
                    args,
                    waitForExit: true);
                server.Stop();

                // Assert
                Assert.Equal(0, r1.Item1);
                
                // verify that only package id & version is displayed
                var expectedOutput = "testPackage1 1.1.0" + Environment.NewLine +
                    "testPackage2 2.1" + Environment.NewLine;
                Assert.Equal(expectedOutput, r1.Item2);
                                
                Assert.Contains("$filter=IsLatestVersion", searchRequest);
                Assert.Contains("searchTerm='test", searchRequest);
                Assert.Contains("includePrerelease=false", searchRequest);
            }
            finally
            {
                // Cleanup
                Util.DeleteDirectory(packageDirectory);
            }
        }

        // Tests that list command only show listed packages
        [Fact(Skip = "This is tracked by an issue")]
        public void ListCommand_OnlyShowListed()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var mockServerEndPoint = "http://localhost:1234/";

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new ZipPackage(packageFileName1);
                var package2 = new ZipPackage(packageFileName2);
                package1.Listed = false;

                var server = new MockServer(mockServerEndPoint);
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
                var args = "--debug list test -Source " + mockServerEndPoint + "nuget";
                var r1 = CommandRunner.Run(
                    nugetexe,
                    tempPath,
                    args,
                    waitForExit: true);
                server.Stop();

                // Assert
                Assert.Equal(0, r1.Item1);

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
            finally
            {
                // Cleanup
                Util.DeleteDirectory(packageDirectory);
            }
        }

        // Tests that list command show delisted packages 
        // when IncludeDelisted is specified.
        [Fact(Skip = "This is tracked by an issue")]
        public void ListCommand_IncludeDelisted()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var mockServerEndPoint = "http://localhost:1234/";

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new ZipPackage(packageFileName1);
                var package2 = new ZipPackage(packageFileName2);
                package1.Listed = false;

                var server = new MockServer(mockServerEndPoint);
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
                var args = "list test -IncludeDelisted -Source " + mockServerEndPoint + "nuget";
                var r1 = CommandRunner.Run(
                    nugetexe,
                    tempPath,
                    args,
                    waitForExit: true);
                server.Stop();

                // Assert
                Assert.Equal(0, r1.Item1);

                // verify that both testPackage1 and testPackage2 are listed.
                var expectedOutput = 
                    "testPackage1 1.1.0" + Environment.NewLine +
                    "testPackage2 2.1" + Environment.NewLine;
                Assert.Equal(expectedOutput, r1.Item2);

                Assert.Contains("$filter=IsLatestVersion", searchRequest);
                Assert.Contains("searchTerm='test", searchRequest);
                Assert.Contains("includePrerelease=false", searchRequest);

                // verify that "includeDelisted=true" is included in the request
                Assert.Contains("includeDelisted=true", searchRequest);
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
            var mockServerEndPoint = "http://localhost:1234/";

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new ZipPackage(packageFileName1);
                var package2 = new ZipPackage(packageFileName2);

                var server = new MockServer(mockServerEndPoint);
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
                var args = "list test -Verbosity detailed -Source " + mockServerEndPoint + "nuget";
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
            finally
            {
                // Cleanup
                Util.DeleteDirectory(packageDirectory);
            }
        }

        // Tests that when -AllVersions is specified, list command sends request 
        // without $filter
        [Fact(Skip = "This is tracked by an issue")]
        public void ListCommand_AllVersions()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var mockServerEndPoint = "http://localhost:1234/";

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new ZipPackage(packageFileName1);
                var package2 = new ZipPackage(packageFileName2);

                var server = new MockServer(mockServerEndPoint);
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
                var args = "list test -AllVersions -Source " + mockServerEndPoint + "nuget";
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
            var mockServerEndPoint = "http://localhost:1234/";

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new ZipPackage(packageFileName1);
                var package2 = new ZipPackage(packageFileName2);

                var server = new MockServer(mockServerEndPoint);
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
                var args = "list test -Prerelease -Source " + mockServerEndPoint + "nuget";
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
            finally
            {
                // Cleanup
                Util.DeleteDirectory(packageDirectory);
            }
        }

        // Test case when both switches -Prerelease and -AllVersions are specified
        [Fact(Skip = "This is tracked by an issue")]
        public void ListCommand_AllVersionsPrerelease()
        {
            var nugetexe = Util.GetNuGetExePath();
            var tempPath = Path.GetTempPath();
            var packageDirectory = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var mockServerEndPoint = "http://localhost:1234/";

            try
            {
                // Arrange
                Util.CreateDirectory(packageDirectory);
                var packageFileName1 = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var packageFileName2 = Util.CreateTestPackage("testPackage2", "2.1", packageDirectory);
                var package1 = new ZipPackage(packageFileName1);
                var package2 = new ZipPackage(packageFileName2);

                var server = new MockServer(mockServerEndPoint);
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
                var args = "list test -AllVersions -Prerelease -Source " + mockServerEndPoint + "nuget";
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
            finally
            {
                // Cleanup
                Util.DeleteDirectory(packageDirectory);
            }
        }
    }
}
