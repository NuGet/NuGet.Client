﻿using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class RestoreRetryTests
    {
        // Restore a packages.config file from a failing v2 http source.
        [Fact(Skip = "https://github.com/NuGet/Home/issues/1217")]
        public void RestoreRetry_PackagesConfigRetryOnFailingV2Source()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var packageDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var package = new ZipPackage(packageFileName);

                Util.CreateFile(
                    workingDirectory,
                    "packages.config",
                    @"<packages>
                        <package id=""testPackage1"" version=""1.1.0"" />
                    </packages>");

                // Server setup
                using (var server = new MockServer())
                {
                    var hitsByUrl = new ConcurrentDictionary<string, int>();

                    server.Get.Add("/", r =>
                    {
                        var path = r.Url.PathAndQuery;

                    // track hits on the url
                    var urlHits = hitsByUrl.AddOrUpdate(path, 1, (s, i) => i + 1);

                        if (path == "/nuget/$metadata")
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                MockServer.SetResponseContent(response, MockServerResource.NuGetV2APIMetadata);
                            });
                        }
                        else if (path == "/package/testPackage1/1.1.0")
                        {
                        // Fail on the first two requests for this download
                        if (urlHits < 3)
                            {
                                return new Action<HttpListenerResponse>(response =>
                                {
                                    response.StatusCode = 503;
                                });
                            }

                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.ContentType = "application/zip";
                                using (var stream = package.GetStream())
                                {
                                    var content = stream.ReadAllBytes();
                                    MockServer.SetResponseContent(response, content);
                                }
                            });
                        }
                        else if (path == "/nuget/Packages(Id='testPackage1',Version='1.1.0')")
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.ContentType = "application/atom+xml;type=entry;charset=utf-8";
                                var odata = server.ToOData(package);
                                MockServer.SetResponseContent(response, odata);
                            });
                        }
                        else if (path == "/nuget")
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.StatusCode = 200;
                            });
                        }

                        throw new Exception("This test needs to be updated to support: " + path);
                    });

                    server.Start();

                    // Act
                    var args = string.Format(
                        "restore packages.config -SolutionDirectory . -Source {0}nuget -NoCache",
                            server.Uri);

                    var timer = new Stopwatch();
                    timer.Start();

                    var r1 = CommandRunner.Run(
                        nugetexe,
                        workingDirectory,
                        args,
                        waitForExit: true);

                    timer.Stop();
                    server.Stop();

                    // Assert
                    Assert.True(Util.IsSuccess(r1), r1.Item2 + " " + r1.Item3);

                    Assert.True(
                        File.Exists(
                            Path.Combine(workingDirectory,
                                "packages/testpackage1.1.1.0/testpackage1.1.1.0.nupkg")));
                }
            }
        }

        // Restore project.json from a failing v2 http source.
        [Fact]
        public void RestoreRetry_ProjectJsonRetryOnFailingV2Source()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var packageDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var package = new ZipPackage(packageFileName);

                Util.CreateFile(
                    workingDirectory,
                    "project.json",
                    @"{
                    ""dependencies"": {
                        ""testPackage1"": ""1.1.0""
                    },
                    ""frameworks"": {
                                ""net45"": { }
                                }
                  }");

                Util.CreateFile(
                    workingDirectory,
                    "nuget.config",
                        @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <configuration>
                          <config>
                            <add key=""globalPackagesFolder"" value=""globalPackages"" />
                          </config>
                        </configuration>");

                // Server setup
                using (var server = new MockServer())
                {
                    var hitsByUrl = new ConcurrentDictionary<string, int>();

                    server.Get.Add("/", r =>
                    {
                        var path = r.Url.PathAndQuery;

                    // track hits on the url
                    var urlHits = hitsByUrl.AddOrUpdate(path, 1, (s, i) => i + 1);

                    // Fail on the first 2 requests for every url
                    if (urlHits < 3)
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.StatusCode = 503;
                            });
                        }

                        if (path == "/nuget/$metadata")
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                MockServer.SetResponseContent(response, MockServerResource.NuGetV2APIMetadata);
                            });
                        }
                        else if (path == "/package/testPackage1/1.1.0")
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.ContentType = "application/zip";
                                using (var stream = package.GetStream())
                                {
                                    var content = stream.ReadAllBytes();
                                    MockServer.SetResponseContent(response, content);
                                }
                            });
                        }
                        else if (path == "/nuget/FindPackagesById()?id='testPackage1'")
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.ContentType = "application/atom+xml;type=feed;charset=utf-8";
                                string feed = server.ToODataFeed(new[] { package }, "FindPackagesById");
                                MockServer.SetResponseContent(response, feed);
                            });
                        }
                        else if (path == "/nuget")
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.StatusCode = 200;
                            });
                        }

                        throw new Exception("This test needs to be updated to support: " + path);
                    });

                    server.Start();

                    // Act
                    var args = string.Format(
                        "restore project.json -SolutionDirectory . -Source {0}nuget -NoCache",
                            server.Uri);

                    var timer = new Stopwatch();
                    timer.Start();

                    var r1 = CommandRunner.Run(
                        nugetexe,
                        workingDirectory,
                        args,
                        waitForExit: true);

                    timer.Stop();

                    server.Stop();

                    // Assert
                    Assert.True(Util.IsSuccess(r1), r1.Item2 + " " + r1.Item3);

                    Assert.True(
                        File.Exists(
                            Path.Combine(workingDirectory,
                                "globalPackages/testpackage1/1.1.0/testpackage1.1.1.0.nupkg")));

                    Assert.True(
                        File.Exists(
                            Path.Combine(workingDirectory,
                                "globalPackages/testpackage1/1.1.0/testPackage1.1.1.0.nupkg.sha512")));

                    Assert.True(File.Exists(Path.Combine(workingDirectory, "project.lock.json")));

                    // Everything should be hit 3 times
                    foreach (var url in hitsByUrl.Keys)
                    {
                        Assert.True(hitsByUrl[url] == 3, url);
                    }
                }
            }
        }

        // Restore project.json from a failing v3 http source.
        [Fact]
        public void RestoreRetry_ProjectJsonRetryOnFailingV3Source()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var packageDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var package = new ZipPackage(packageFileName);

                Util.CreateFile(
                    workingDirectory,
                    "project.json",
                    @"{
                    ""dependencies"": {
                        ""testPackage1"": ""1.1.0""
                    },
                    ""frameworks"": {
                                ""net45"": { }
                                }
                  }");

                Util.CreateFile(
                    workingDirectory,
                    "nuget.config",
                        @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <configuration>
                          <config>
                            <add key=""globalPackagesFolder"" value=""globalPackages"" />
                          </config>
                        </configuration>");

                // Server setup
                var indexJson = Util.CreateIndexJson();

                using (var server = new MockServer())
                {
                    Util.AddFlatContainerResource(indexJson, server);
                    Util.AddRegistrationResource(indexJson, server);
                    var hitsByUrl = new ConcurrentDictionary<string, int>();

                    server.Get.Add("/", r =>
                    {
                        var path = r.Url.AbsolutePath;

                    // track hits on the url
                    var urlHits = hitsByUrl.AddOrUpdate(path, 1, (s, i) => i + 1);

                    // Fail on the first 2 requests for every url
                    if (urlHits < 3)
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.StatusCode = 503;
                            });
                        }

                        if (path == "/index.json")
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.StatusCode = 200;
                                response.ContentType = "text/javascript";
                                MockServer.SetResponseContent(response, indexJson.ToString());
                            });
                        }
                        else if (path == "/flat/testpackage1/1.1.0/testpackage1.1.1.0.nupkg")
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.ContentType = "application/zip";
                                using (var stream = package.GetStream())
                                {
                                    var content = stream.ReadAllBytes();
                                    MockServer.SetResponseContent(response, content);
                                }
                            });
                        }
                        else if (path == "/flat/testpackage1/index.json")
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.ContentType = "text/javascript";

                                MockServer.SetResponseContent(response, @"{
                              ""versions"": [
                                ""0.1.0"",
                                ""0.3.0"",
                                ""0.4.0"",
                                ""0.5.0"",
                                ""1.0.0"",
                                ""1.1.0"",
                                ""1.2.0""
                              ]
                            }");
                            });
                        }

                        throw new Exception("This test needs to be updated to support: " + path);
                    });

                    server.Start();

                    // The minimum time is the number of urls x 3 waits x 200ms
                    var minTime = TimeSpan.FromMilliseconds(hitsByUrl.Count * 3 * 200);

                    // Act
                    var args = string.Format(
                        "restore project.json -SolutionDirectory . -Source {0}index.json -NoCache",
                            server.Uri);

                    var timer = new Stopwatch();
                    timer.Start();

                    var r1 = CommandRunner.Run(
                        nugetexe,
                        workingDirectory,
                        args,
                        waitForExit: true);

                    timer.Stop();

                    server.Stop();

                    // Assert
                    Assert.True(Util.IsSuccess(r1), r1.Item2 + " " + r1.Item3);

                    Assert.True(
                        File.Exists(
                            Path.Combine(workingDirectory,
                                "globalPackages/testpackage1/1.1.0/testpackage1.1.1.0.nupkg")));

                    Assert.True(
                        File.Exists(
                            Path.Combine(workingDirectory,
                                "globalPackages/testpackage1/1.1.0/testPackage1.1.1.0.nupkg.sha512")));

                    Assert.True(File.Exists(Path.Combine(workingDirectory, "project.lock.json")));

                    // Everything should be hit 3 times
                    foreach (var url in hitsByUrl.Keys)
                    {
                        Assert.True(hitsByUrl[url] == 3, url);
                    }

                    Assert.True(timer.Elapsed > minTime);
                }
            }
        }

        // Restore packages.config from a failing v3 http source.
        [Fact]
        public void RestoreRetry_PackagesConfigRetryOnFailingV3Source()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var packageDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);
                var package = new ZipPackage(packageFileName);

                Util.CreateFile(
                    workingDirectory,
                    "packages.config",
                    @"<packages>
                    <package id=""testPackage1"" version=""1.1.0"" />
                  </packages>");

                Util.CreateFile(
                    workingDirectory,
                    "nuget.config",
                        @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <configuration>
                            <config>
                            <add key=""globalPackagesFolder"" value=""globalPackages"" />
                            </config>
                        </configuration>");

                // Server setup
                var indexJson = Util.CreateIndexJson();

                using (var server = new MockServer())
                {
                    Util.AddFlatContainerResource(indexJson, server);
                    Util.AddRegistrationResource(indexJson, server);
                    var hitsByUrl = new ConcurrentDictionary<string, int>();

                    server.Get.Add("/", r =>
                    {
                        var path = r.Url.AbsolutePath;

                    // track hits on the url
                    var urlHits = hitsByUrl.AddOrUpdate(path, 1, (s, i) => i + 1);

                    // Fail on the first 2 requests for every url
                    if (urlHits < 3)
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.StatusCode = 503;
                            });
                        }

                        if (path == "/index.json")
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.StatusCode = 200;
                                response.ContentType = "text/javascript";
                                MockServer.SetResponseContent(response, indexJson.ToString());
                            });
                        }
                        else if (path == "/flat/testpackage1/1.1.0/testpackage1.1.1.0.nupkg")
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.ContentType = "application/zip";
                                using (var stream = package.GetStream())
                                {
                                    var content = stream.ReadAllBytes();
                                    MockServer.SetResponseContent(response, content);
                                }
                            });
                        }
                        else if (path == "/reg/testpackage1/index.json")
                        {
                            return new Action<HttpListenerResponse>(response =>
                            {
                                response.ContentType = "text/javascript";

                                string json = null;

                                json = @"{
                    ""@id"": ""{0}/reg/testPackage1/index.json"",
                    ""@type"": [
                    ""catalog:CatalogRoot"",
                    ""PackageRegistration"",
                    ""catalog:Permalink""
                    ],
                    ""commitId"": ""6d2d2375-b263-49ee-9a46-fd6b2d77e592"",
                    ""commitTimeStamp"": ""2015-06-22T22:30:00.1487642Z"",
                    ""count"": 1,
                    ""items"": [
                    {
                        ""@id"": ""{0}reg/testPackage1/index.json#page/0.0.0/9.0.0"",
                        ""@type"": ""catalog:CatalogPage"",
                        ""commitId"": ""6d2d2375-b263-49ee-9a46-fd6b2d77e592"",
                        ""commitTimeStamp"": ""2015-06-22T22:30:00.1487642Z"",
                        ""count"": 1,
                        ""items"": [
                        {
                            ""@id"": ""{0}reg/testPackage1/1.1.0.json"",
                            ""@type"": ""Package"",
                            ""commitId"": ""1fa214b1-6a03-4b4e-a16e-4925f994057f"",
                            ""commitTimeStamp"": ""2015-04-01T20:27:37.8431747Z"",
                            ""catalogEntry"": {
                            ""@id"": ""{0}catalog0/data/2015.02.01.06.24.15/testPackage1.1.1.0.json"",
                            ""@type"": ""PackageDetails"",
                            ""authors"": ""test master"",
                            ""description"": ""test one"",
                            ""iconUrl"": """",
                            ""id"": ""testPackage1"",
                            ""language"": ""en-US"",
                            ""licenseUrl"": """",
                            ""listed"": true,
                            ""minClientVersion"": """",
                            ""projectUrl"": """",
                            ""published"": ""2012-01-01T22:12:57.713Z"",
                            ""requireLicenseAcceptance"": false,
                            ""summary"": ""stuffs"",
                            ""tags"": [
                                """"
                            ],
                            ""title"": """",
                            ""version"": ""1.1.0""
                            },
                            ""packageContent"": ""{0}packages/testPackage1.1.1.0.nupkg"",
                            ""registration"": ""{0}reg/testPackage1/index.json""
                        }],
                ""parent"": ""{0}reg/testPackage1/index.json"",
                        ""lower"": ""0.0.0"",
                        ""upper"": ""9.0.0""
                    }
                    ]}".Replace("{0}", server.Uri);

                                var jObject = JObject.Parse(json);

                                MockServer.SetResponseContent(response, jObject.ToString());
                            });
                        }

                        throw new Exception("This test needs to be updated to support: " + path);
                    });

                    server.Start();

                    // The minimum time is the number of urls x 3 waits x 200ms
                    var minTime = TimeSpan.FromMilliseconds(hitsByUrl.Count * 3 * 200);

                    // Act
                    var args = string.Format(
                        "restore packages.config -SolutionDirectory . -Source {0}index.json -NoCache",
                            server.Uri);

                    var timer = new Stopwatch();
                    timer.Start();

                    var r1 = CommandRunner.Run(
                        nugetexe,
                        workingDirectory,
                        args,
                        waitForExit: true);

                    timer.Stop();

                    server.Stop();

                    // Assert
                    Assert.True(Util.IsSuccess(r1), r1.Item2 + " " + r1.Item3);

                    Assert.True(
                        File.Exists(
                            Path.Combine(workingDirectory,
                                "packages/testpackage1.1.1.0/testpackage1.1.1.0.nupkg")));

                    // Everything should be hit 3 times
                    foreach (var url in hitsByUrl.Keys)
                    {
                        Assert.True(hitsByUrl[url] == 3, url);
                    }

                    Assert.True(timer.Elapsed > minTime);
                }
            }
        }
    }
}
