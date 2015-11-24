using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NetworkCallCountTest : IDisposable
    {
        [Fact]
        public void NetworkCallCount_RestoreLargePackagesConfigWithMultipleSourcesWithAllMissingPackages()
        {
            // Arrange
            var testCount = 100;

            using (var server2 = new MockServer())
            using (var server = new MockServer())
            {
                var tempPath = Path.GetTempPath();
                var guid = Guid.NewGuid();
                var workingPath = Path.Combine(tempPath, guid.ToString());
                _dirs.TryAdd(workingPath, false);
                var repositoryPath = Path.Combine(workingPath, "repo");
                var repositoryPath2 = Path.Combine(workingPath, "repo2");
                var repositoryPath3 = Path.Combine(workingPath, "repo3");
                var allRepo = Path.Combine(workingPath, "allRepo");
                var packagesFolderPath = Path.Combine(workingPath, "packages");

                Directory.CreateDirectory(workingPath);
                Directory.CreateDirectory(allRepo);
                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(repositoryPath2);
                Directory.CreateDirectory(repositoryPath3);
                Directory.CreateDirectory(packagesFolderPath);

                var packagesConfigPath = Path.Combine(workingPath, "packages.config");

                var doc = new XDocument();
                var root = new XElement(XName.Get("packages"));
                doc.Add(root);

                var expectedPackages = new HashSet<PackageIdentity>();

                for (int i = 0; i < testCount; i++)
                {
                    var id = $"package{i}";
                    var version = $"1.0.{i}";

                    var entry = new XElement(XName.Get("package"));
                    entry.Add(new XAttribute(XName.Get("id"), id));
                    entry.Add(new XAttribute(XName.Get("version"), version));

                    root.Add(entry);
                }

                Util.CreateFile(workingPath, "packages.config", doc.ToString());

                var nugetexe = Util.GetNuGetExePath();
                var serverRepo = new LocalPackageRepository(repositoryPath);
                var server2Repo = new LocalPackageRepository(repositoryPath2);
                var allPackageRepo = new LocalPackageRepository(allRepo);

                // Server setup
                var indexJson = Util.CreateIndexJson();
                var indexJson2 = Util.CreateIndexJson();

                Util.AddFlatContainerResource(indexJson, server);
                Util.AddRegistrationResource(indexJson, server);
                var hitsByUrl = new ConcurrentDictionary<string, int>();

                Util.AddFlatContainerResource(indexJson2, server2);
                Util.AddRegistrationResource(indexJson2, server2);
                var hitsByUrl2 = new ConcurrentDictionary<string, int>();

                server.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl, server, indexJson, serverRepo);
                });

                server2.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl2, server2, indexJson2, server2Repo);
                });

                server.Start();
                server2.Start();

                var sources = new List<string>() { server2.Uri + "nuget", server.Uri + "index.json", repositoryPath3 };
                Util.CreateNuGetConfig(workingPath, sources);

                string[] args = new string[] {
                    "restore",
                    packagesConfigPath,
                    "-DisableParallelProcessing"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true,
                    timeOutInMilliseconds: (int)TimeSpan.FromMinutes(3).TotalMilliseconds);

                server.Stop();
                server2.Stop();

                var packagesFolder = new LocalPackageRepository(packagesFolderPath);
                var allPackages = packagesFolder.GetPackages().ToList();

                // Assert
                Assert.True(0 != r.Item1, r.Item2 + " " + r.Item3);

                Assert.Equal(1, hitsByUrl["/index.json"]);
                Assert.Equal(1, hitsByUrl2["/nuget"]);

                Assert.Equal(0, allPackages.Count());
                Assert.Equal(0,
                    Directory.Exists(MachineCache.Default.Source) ?
                    Directory.GetFiles(MachineCache.Default.Source, "*.tmp").Count()
                    : 0);
            }
        }

        [Fact]
        public void NetworkCallCount_RestoreLargePackagesConfigWithMultipleSourcesWithPartialMissingPackages()
        {
            // Arrange
            var testCount = 100;

            using (var server2 = new MockServer())
            using (var server = new MockServer())
            {
                var tempPath = Path.GetTempPath();
                var guid = Guid.NewGuid();
                var workingPath = Path.Combine(tempPath, guid.ToString());
                _dirs.TryAdd(workingPath, false);
                var repositoryPath = Path.Combine(workingPath, "repo");
                var repositoryPath2 = Path.Combine(workingPath, "repo2");
                var repositoryPath3 = Path.Combine(workingPath, "repo3");
                var allRepo = Path.Combine(workingPath, "allRepo");
                var packagesFolderPath = Path.Combine(workingPath, "packages");
                MachineCache.Default.Clear();

                Directory.CreateDirectory(workingPath);
                Directory.CreateDirectory(allRepo);
                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(repositoryPath2);
                Directory.CreateDirectory(repositoryPath3);
                Directory.CreateDirectory(packagesFolderPath);

                var packagesConfigPath = Path.Combine(workingPath, "packages.config");

                var doc = new XDocument();
                var root = new XElement(XName.Get("packages"));
                doc.Add(root);

                var expectedPackages = new HashSet<PackageIdentity>();

                for (int i = 0; i < testCount; i++)
                {
                    var id = $"package{i}";
                    var version = $"1.0.{i}";

                    var entry = new XElement(XName.Get("package"));
                    entry.Add(new XAttribute(XName.Get("id"), id));
                    entry.Add(new XAttribute(XName.Get("version"), version));

                    if (i % 3 == 0)
                    {
                        // Missing
                    }
                    else if (i % 25 == 0)
                    {
                        Util.CreatePackage(repositoryPath3, id, version);
                    }
                    else if (i % 10 == 0)
                    {
                        Util.CreatePackage(repositoryPath, id, version);
                        Util.CreatePackage(repositoryPath2, id, version);
                    }
                    else
                    {
                        Util.CreatePackage(repositoryPath, id, version);
                    }

                    if (i % 3 != 0)
                    {
                        // Add to the all repo also if it is not missing
                        Util.CreatePackage(allRepo, id, version);
                        expectedPackages.Add(new PackageIdentity(id, new NuGetVersion(version)));
                    }

                    root.Add(entry);
                }

                Util.CreateFile(workingPath, "packages.config", doc.ToString());

                var nugetexe = Util.GetNuGetExePath();
                var serverRepo = new LocalPackageRepository(repositoryPath);
                var server2Repo = new LocalPackageRepository(repositoryPath2);
                var allPackageRepo = new LocalPackageRepository(allRepo);

                // Server setup
                var indexJson = Util.CreateIndexJson();
                var indexJson2 = Util.CreateIndexJson();

                Util.AddFlatContainerResource(indexJson, server);
                Util.AddRegistrationResource(indexJson, server);
                var hitsByUrl = new ConcurrentDictionary<string, int>();

                Util.AddFlatContainerResource(indexJson2, server2);
                Util.AddRegistrationResource(indexJson2, server2);
                var hitsByUrl2 = new ConcurrentDictionary<string, int>();

                server.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl, server, indexJson, serverRepo);
                });

                server2.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl2, server2, indexJson2, server2Repo);
                });

                server.Start();
                server2.Start();

                var sources = new List<string>() { server2.Uri + "nuget", server.Uri + "index.json", repositoryPath3 };
                Util.CreateNuGetConfig(workingPath, sources);

                string[] args = new string[] {
                    "restore",
                    packagesConfigPath,
                    "-DisableParallelProcessing"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true,
                    timeOutInMilliseconds: (int)TimeSpan.FromMinutes(3).TotalMilliseconds);

                server.Stop();
                server2.Stop();

                var packagesFolder = new LocalPackageRepository(packagesFolderPath);
                var allPackages = packagesFolder.GetPackages().ToList();

                // Assert
                Assert.True(0 != r.Item1, r.Item2 + " " + r.Item3);

                Assert.Equal(1, hitsByUrl["/index.json"]);
                Assert.Equal(1, hitsByUrl2["/nuget"]);

                Assert.Equal(expectedPackages.Count, allPackages.Count());

                foreach (var package in expectedPackages)
                {
                    Assert.True(allPackages.Any(p => p.Id == package.Id
                        && p.Version.ToNormalizedString() == package.Version.ToNormalizedString()));
                }

                Assert.Equal(0, Directory.GetFiles(MachineCache.Default.Source, "*.tmp").Count());
            }
        }

        [Fact]
        public void NetworkCallCount_RestoreLargePackagesConfigWithMultipleSourcesMainlyV3()
        {
            // Arrange
            var testCount = 100;

            using (var server2 = new MockServer())
            using (var server = new MockServer())
            {
                var tempPath = Path.GetTempPath();
                var guid = Guid.NewGuid();
                var workingPath = Path.Combine(tempPath, guid.ToString());
                _dirs.TryAdd(workingPath, false);
                var repositoryPath = Path.Combine(workingPath, "repo");
                var repositoryPath2 = Path.Combine(workingPath, "repo2");
                var repositoryPath3 = Path.Combine(workingPath, "repo3");
                var allRepo = Path.Combine(workingPath, "allRepo");
                var packagesFolderPath = Path.Combine(workingPath, "packages");
                MachineCache.Default.Clear();

                Directory.CreateDirectory(workingPath);
                Directory.CreateDirectory(allRepo);
                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(repositoryPath2);
                Directory.CreateDirectory(repositoryPath3);
                Directory.CreateDirectory(packagesFolderPath);

                var packagesConfigPath = Path.Combine(workingPath, "packages.config");

                var doc = new XDocument();
                var root = new XElement(XName.Get("packages"));
                doc.Add(root);

                var expectedPackages = new HashSet<PackageIdentity>();

                for (int i=0; i < testCount; i++)
                {
                    var id = $"package{i}";
                    var version = $"1.0.{i}";

                    var entry = new XElement(XName.Get("package"));
                    entry.Add(new XAttribute(XName.Get("id"), id));
                    entry.Add(new XAttribute(XName.Get("version"), version));

                    if (i % 25 == 0)
                    {
                        Util.CreatePackage(repositoryPath3, id, version);
                    }
                    else if (i % 10 == 0)
                    {
                        Util.CreatePackage(repositoryPath, id, version);
                        Util.CreatePackage(repositoryPath2, id, version);
                    }
                    else
                    {
                        Util.CreatePackage(repositoryPath, id, version);
                    }

                    // Add to the all repo also
                    Util.CreatePackage(allRepo, id, version);

                    root.Add(entry);

                    expectedPackages.Add(new PackageIdentity(id, new NuGetVersion(version)));
                }

                Util.CreateFile(workingPath, "packages.config", doc.ToString());

                var nugetexe = Util.GetNuGetExePath();
                var serverRepo = new LocalPackageRepository(repositoryPath);
                var server2Repo = new LocalPackageRepository(repositoryPath2);

                // Server setup
                var indexJson = Util.CreateIndexJson();
                var indexJson2 = Util.CreateIndexJson();

                Util.AddFlatContainerResource(indexJson, server);
                Util.AddRegistrationResource(indexJson, server);
                var hitsByUrl = new ConcurrentDictionary<string, int>();

                Util.AddFlatContainerResource(indexJson2, server2);
                Util.AddRegistrationResource(indexJson2, server2);
                var hitsByUrl2 = new ConcurrentDictionary<string, int>();

                server.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl, server, indexJson, serverRepo);
                });

                server2.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl2, server2, indexJson2, server2Repo);
                });

                server.Start();
                server2.Start();

                var sources = new List<string>() { server2.Uri + "nuget", server.Uri + "index.json", repositoryPath3 };
                Util.CreateNuGetConfig(workingPath, sources);

                string[] args = new string[] {
                    "restore",
                    packagesConfigPath,
                    "-DisableParallelProcessing"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true,
                    timeOutInMilliseconds: (int)TimeSpan.FromMinutes(3).TotalMilliseconds);

                server.Stop();
                server2.Stop();

                var packagesFolder = new LocalPackageRepository(packagesFolderPath);
                var allPackages = packagesFolder.GetPackages().ToList();

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                Assert.Equal(testCount, allPackages.Count());

                foreach (var package in expectedPackages)
                {
                    Assert.True(allPackages.Any(p => p.Id == package.Id
                        && p.Version.ToNormalizedString() == package.Version.ToNormalizedString()));
                }

                Assert.Equal(0, Directory.GetFiles(MachineCache.Default.Source, "*.tmp").Count());
            }
        }

        [Fact]
        public void NetworkCallCount_RestoreLargePackagesConfigWithMultipleSourcesMainlyV2()
        {
            // Arrange
            var testCount = 100;

            using (var server2 = new MockServer())
            using (var server = new MockServer())
            {
                var tempPath = Path.GetTempPath();
                var guid = Guid.NewGuid();
                var workingPath = Path.Combine(tempPath, guid.ToString());
                _dirs.TryAdd(workingPath, false);
                var repositoryPath = Path.Combine(workingPath, "repo");
                var repositoryPath2 = Path.Combine(workingPath, "repo2");
                var repositoryPath3 = Path.Combine(workingPath, "repo3");
                var allRepo = Path.Combine(workingPath, "allRepo");
                var packagesFolderPath = Path.Combine(workingPath, "packages");
                MachineCache.Default.Clear();

                Directory.CreateDirectory(workingPath);
                Directory.CreateDirectory(allRepo);
                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(repositoryPath2);
                Directory.CreateDirectory(repositoryPath3);
                Directory.CreateDirectory(packagesFolderPath);

                var packagesConfigPath = Path.Combine(workingPath, "packages.config");

                var doc = new XDocument();
                var root = new XElement(XName.Get("packages"));
                doc.Add(root);

                var expectedPackages = new HashSet<PackageIdentity>();

                for (int i = 0; i < testCount; i++)
                {
                    var id = $"package{i}";
                    var version = $"1.0.{i}";

                    var entry = new XElement(XName.Get("package"));
                    entry.Add(new XAttribute(XName.Get("id"), id));
                    entry.Add(new XAttribute(XName.Get("version"), version));

                    if (i % 25 == 0)
                    {
                        Util.CreatePackage(repositoryPath3, id, version);
                    }
                    else if (i % 10 == 0)
                    {
                        Util.CreatePackage(repositoryPath, id, version);
                        Util.CreatePackage(repositoryPath2, id, version);
                    }
                    else
                    {
                        Util.CreatePackage(repositoryPath, id, version);
                    }

                    // Add to the all repo also
                    Util.CreatePackage(allRepo, id, version);

                    root.Add(entry);

                    expectedPackages.Add(new PackageIdentity(id, new NuGetVersion(version)));
                }

                Util.CreateFile(workingPath, "packages.config", doc.ToString());

                var nugetexe = Util.GetNuGetExePath();
                var serverRepo = new LocalPackageRepository(repositoryPath);
                var server2Repo = new LocalPackageRepository(repositoryPath2);

                // Server setup
                var indexJson = Util.CreateIndexJson();
                var indexJson2 = Util.CreateIndexJson();

                Util.AddFlatContainerResource(indexJson, server);
                Util.AddRegistrationResource(indexJson, server);
                var hitsByUrl = new ConcurrentDictionary<string, int>();

                Util.AddFlatContainerResource(indexJson2, server2);
                Util.AddRegistrationResource(indexJson2, server2);
                var hitsByUrl2 = new ConcurrentDictionary<string, int>();

                server.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl, server, indexJson, serverRepo);
                });

                server2.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl2, server2, indexJson2, server2Repo);
                });

                server.Start();
                server2.Start();

                var sources = new List<string>() { server2.Uri + "index.json", server.Uri + "nuget", repositoryPath3 };
                Util.CreateNuGetConfig(workingPath, sources);

                string[] args = new string[] {
                    "restore",
                    packagesConfigPath,
                    "-DisableParallelProcessing"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true,
                    timeOutInMilliseconds: (int)TimeSpan.FromMinutes(3).TotalMilliseconds);

                server.Stop();
                server2.Stop();

                var packagesFolder = new LocalPackageRepository(packagesFolderPath);
                var allPackages = packagesFolder.GetPackages().ToList();

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                Assert.Equal(testCount, allPackages.Count());

                foreach (var package in expectedPackages)
                {
                    Assert.True(allPackages.Any(p => p.Id == package.Id
                        && p.Version.ToNormalizedString() == package.Version.ToNormalizedString()));
                }

                Assert.Equal(0, Directory.GetFiles(MachineCache.Default.Source, "*.tmp").Count());
            }
        }

        [Fact]
        public void NetworkCallCount_CancelPackageDownloadForV3()
        {
            // Arrange
            using (var server2 = new MockServer())
            using (var server = new MockServer())
            {
                var workingDir = CreateMixedConfigAndJson();
                var workingPath = workingDir.FullName;
                var repositoryPath = Path.Combine(workingPath, "repo");
                var slnPath = Path.Combine(workingPath, "test.sln");
                var nugetexe = Util.GetNuGetExePath();
                var localRepo = new LocalPackageRepository(repositoryPath);

                var packagesFolder =
                    new DirectoryInfo(Path.Combine(workingPath, "packages"));
                packagesFolder.Create();

                var globalFolder =
                    new DirectoryInfo(Path.Combine(workingPath, "globalPackages"));

                // Server setup
                var indexJson = Util.CreateIndexJson();
                var indexJson2 = Util.CreateIndexJson();

                Util.AddFlatContainerResource(indexJson, server);
                Util.AddRegistrationResource(indexJson, server);

                Util.AddFlatContainerResource(indexJson2, server2);
                Util.AddRegistrationResource(indexJson2, server2);

                var hitsByUrl = new ConcurrentDictionary<string, int>();
                var hitsByUrl2 = new ConcurrentDictionary<string, int>();

                var v2ResetEvent = new ManualResetEventSlim(true);
                var v3ResetEvent = new ManualResetEventSlim(false);

                server.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl, server, indexJson, localRepo, v2ResetEvent, v3ResetEvent);
                });

                server2.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl2, server2, indexJson2, localRepo, v2ResetEvent, v3ResetEvent);
                });

                server.Start();
                server2.Start();
                var sources = new List<string>() { server.Uri + "index.json", server2.Uri + "nuget" };
                Util.CreateNuGetConfig(workingPath, sources);

                string[] args = new string[] {
                    "restore",
                    slnPath,
                    "-Verbosity",
                    "detailed",
                    "-DisableParallelProcessing"
                };

                // Delete the entire machine cache
                MachineCache.Default.Clear();

                var task = Task.Run(() =>
                {
                    // Wait until all packages exist before allowing v2 to return
                    while (packagesFolder.GetDirectories("*", SearchOption.TopDirectoryOnly).Count() < 3)
                    {
                        Thread.Sleep(100);
                    }

                    v3ResetEvent.Set();
                });

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                task.Wait();

                var globalFolderCount = Directory.GetDirectories(
                    globalFolder.FullName, "*", SearchOption.TopDirectoryOnly)
                    .Count();
                var machineCacheCount = Directory.GetFiles(MachineCache.Default.Source).Count();
                var packagesFolderCount = Directory.GetDirectories(
                    packagesFolder.FullName, "*", SearchOption.TopDirectoryOnly)
                    .Count();

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                // The machine cache should be used for everything.
                Assert.Equal(3, machineCacheCount);
                Assert.Equal(3, packagesFolderCount);   // project.json packages still go here
                Assert.Equal(3, globalFolderCount);     // packages.config packages should have gone in v2
            }
        }

        [Fact]
        public void NetworkCallCount_CancelPackageDownloadForV2()
        {
            // Arrange
            using (var server2 = new MockServer())
            using (var server = new MockServer())
            {
                var workingDir = CreateMixedConfigAndJson();
                var workingPath = workingDir.FullName;
                var repositoryPath = Path.Combine(workingPath, "repo");
                var slnPath = Path.Combine(workingPath, "test.sln");
                var nugetexe = Util.GetNuGetExePath();
                var localRepo = new LocalPackageRepository(repositoryPath);

                var packagesFolder =
                    new DirectoryInfo(Path.Combine(workingPath, "packages"));
                packagesFolder.Create();

                var globalFolder =
                    new DirectoryInfo(Path.Combine(workingPath, "globalPackages"));

                // Server setup
                var indexJson = Util.CreateIndexJson();

                Util.AddFlatContainerResource(indexJson, server);
                Util.AddRegistrationResource(indexJson, server);
                var hitsByUrl = new ConcurrentDictionary<string, int>();
                var hitsByUrl2 = new ConcurrentDictionary<string, int>();

                var v2ResetEvent = new ManualResetEventSlim(false);
                var v3ResetEvent = new ManualResetEventSlim(true);

                server.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl, server, indexJson, localRepo, v2ResetEvent, v3ResetEvent);
                });

                server2.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl2, server2, indexJson, localRepo, v2ResetEvent, v3ResetEvent);
                });

                server.Start();
                server2.Start();
                var sources = new List<string>() { server.Uri + "index.json", server2.Uri + "nuget" };
                Util.CreateNuGetConfig(workingPath, sources);

                string[] args = new string[] {
                    "restore",
                    slnPath,
                    "-Verbosity",
                    "detailed",
                    "-DisableParallelProcessing"
                };

                // Delete the entire machine cache
                MachineCache.Default.Clear();

                var task = Task.Run(() =>
                {
                    // Wait until all packages exist before allowing v2 to return
                    while (packagesFolder.GetDirectories("*", SearchOption.TopDirectoryOnly).Count() < 3)
                    {
                        Thread.Sleep(100);
                    }

                    v2ResetEvent.Set();
                });

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true,
                    timeOutInMilliseconds: int.MaxValue);

                task.Wait();

                var globalFolderCount = Directory.GetDirectories(
                    globalFolder.FullName, "*", SearchOption.TopDirectoryOnly)
                    .Count();
                var machineCacheCount = GetMachineCacheCount();
                var packagesFolderCount = Directory.GetDirectories(
                    packagesFolder.FullName, "*", SearchOption.TopDirectoryOnly)
                    .Count();

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                // The machine cache should not have been used since v3 was first.
                Assert.Equal(0, machineCacheCount);
                Assert.Equal(3, packagesFolderCount);
                Assert.Equal(6, globalFolderCount);
            }
        }

        [Fact]
        public void NetworkCallCount_RestoreSolutionMultipleSourcesV2V3AndLocal()
        {
            // Arrange
            using (var server2 = new MockServer())
            using (var server = new MockServer())
            {
                var workingDir = CreateMixedConfigAndJson();
                var workingPath = workingDir.FullName;
                var repositoryPath = Path.Combine(workingPath, "repo");
                var slnPath = Path.Combine(workingPath, "test.sln");
                var nugetexe = Util.GetNuGetExePath();
                var localRepo = new LocalPackageRepository(repositoryPath);

                // Server setup
                var indexJson = Util.CreateIndexJson();
                var indexJson2 = Util.CreateIndexJson();

                Util.AddFlatContainerResource(indexJson, server);
                Util.AddRegistrationResource(indexJson, server);

                Util.AddFlatContainerResource(indexJson2, server2);
                Util.AddRegistrationResource(indexJson2, server2);

                var hitsByUrl = new ConcurrentDictionary<string, int>();
                var hitsByUrl2 = new ConcurrentDictionary<string, int>();

                server.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl, server, indexJson, localRepo);
                });

                server2.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl2, server2, indexJson2, localRepo);
                });

                server.Start();
                server2.Start();

                var sources = new List<string>() { server.Uri + "index.json", server2.Uri + "nuget", repositoryPath };
                Util.CreateNuGetConfig(workingPath, sources);

                string[] args = new string[] {
                    "restore",
                    slnPath,
                    "-Verbosity",
                    "detailed",
                    "-DisableParallelProcessing"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                // Network calls can happen multiple times here with cancelation
                foreach (var url in hitsByUrl.Keys)
                {
                    Assert.True(2 >= hitsByUrl[url], url + " " + hitsByUrl[url]);
                }

                foreach (var url in hitsByUrl2.Keys)
                {
                    Assert.True(2 >= hitsByUrl2[url], url + " " + hitsByUrl2[url]);
                }
            }
        }

        [Fact]
        public void NetworkCallCount_InstallVersionFromV2()
        {
            // Arrange
            using (var server = new MockServer())
            {
                var workingDir = CreateMixedConfigAndJson();
                var workingPath = workingDir.FullName;
                var repositoryPath = Path.Combine(workingPath, "repo");
                var slnPath = Path.Combine(workingPath, "test.sln");
                var nugetexe = Util.GetNuGetExePath();
                var localRepo = new LocalPackageRepository(repositoryPath);
                var outputPath = Path.Combine(workingDir.FullName, "output");
                Directory.CreateDirectory(outputPath);

                var packageA = Util.CreatePackage(repositoryPath, "packageA", "1.0.0");

                // Server setup
                var indexJson = Util.CreateIndexJson();

                Util.AddFlatContainerResource(indexJson, server);
                Util.AddRegistrationResource(indexJson, server);
                var hitsByUrl = new ConcurrentDictionary<string, int>();

                server.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl, server, indexJson, localRepo);
                });

                server.Start();
                var sources = new List<string>() { server.Uri + "nuget" };
                Util.CreateNuGetConfig(workingPath, sources);

                string[] args = new string[] {
                    "install",
                    "packageA",
                    "-Version",
                    "1.0.0",
                    "-OutputDirectory",
                    outputPath,
                    "-Verbosity",
                    "detailed",
                    "-DisableParallelProcessing"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                foreach (var url in hitsByUrl.Keys)
                {
                    Assert.True(1 == hitsByUrl[url], url);
                }
            }
        }

        [Fact]
        public void NetworkCallCount_InstallVersionFromV3()
        {
            // Arrange
            using (var server = new MockServer())
            {
                var workingDir = CreateMixedConfigAndJson();
                var workingPath = workingDir.FullName;
                var repositoryPath = Path.Combine(workingPath, "repo");
                var slnPath = Path.Combine(workingPath, "test.sln");
                var nugetexe = Util.GetNuGetExePath();
                var localRepo = new LocalPackageRepository(repositoryPath);
                var outputPath = Path.Combine(workingDir.FullName, "output");
                Directory.CreateDirectory(outputPath);

                var packageA = Util.CreatePackage(repositoryPath, "packageA", "1.0.0");

                // Server setup
                var indexJson = Util.CreateIndexJson();

                Util.AddFlatContainerResource(indexJson, server);
                Util.AddRegistrationResource(indexJson, server);
                var hitsByUrl = new ConcurrentDictionary<string, int>();

                server.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl, server, indexJson, localRepo);
                });

                server.Start();
                var sources = new List<string>() { server.Uri + "index.json" };
                Util.CreateNuGetConfig(workingPath, sources);

                string[] args = new string[] {
                    "install",
                    "packageA",
                    "-Version",
                    "1.0.0",
                    "-OutputDirectory",
                    outputPath,
                    "-Verbosity",
                    "detailed",
                    "-DisableParallelProcessing"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
                Assert.Equal(1, hitsByUrl["/index.json"]);
                Assert.Equal(1, hitsByUrl["/reg/packagea/index.json"]);

                foreach (var url in hitsByUrl.Keys)
                {
                    Assert.True(1 == hitsByUrl[url], url);
                }
            }
        }

        [Fact]
        public void NetworkCallCount_InstallLatestFromV2()
        {
            // Arrange
            using (var server = new MockServer())
            {
                var workingDir = CreateMixedConfigAndJson();
                var workingPath = workingDir.FullName;
                var repositoryPath = Path.Combine(workingPath, "repo");
                var slnPath = Path.Combine(workingPath, "test.sln");
                var nugetexe = Util.GetNuGetExePath();
                var localRepo = new LocalPackageRepository(repositoryPath);
                var outputPath = Path.Combine(workingDir.FullName, "output");
                Directory.CreateDirectory(outputPath);

                var packageA = Util.CreatePackage(repositoryPath, "packageA", "1.0.0");

                // Server setup
                var indexJson = Util.CreateIndexJson();

                Util.AddFlatContainerResource(indexJson, server);
                Util.AddRegistrationResource(indexJson, server);
                var hitsByUrl = new ConcurrentDictionary<string, int>();

                server.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl, server, indexJson, localRepo);
                });

                server.Start();
                var sources = new List<string>() { server.Uri + "nuget" };
                Util.CreateNuGetConfig(workingPath, sources);

                string[] args = new string[] {
                    "install",
                    "packageA",
                    "-OutputDirectory",
                    outputPath,
                    "-Verbosity",
                    "detailed",
                    "-DisableParallelProcessing"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                foreach (var url in hitsByUrl.Keys)
                {
                    Assert.True(1 == hitsByUrl[url], url);
                }
            }
        }

        [Fact]
        public void NetworkCallCount_InstallLatestFromV3()
        {
            // Arrange
            using (var server = new MockServer())
            {
                var workingDir = CreateMixedConfigAndJson();
                var workingPath = workingDir.FullName;
                var repositoryPath = Path.Combine(workingPath, "repo");
                var slnPath = Path.Combine(workingPath, "test.sln");
                var nugetexe = Util.GetNuGetExePath();
                var localRepo = new LocalPackageRepository(repositoryPath);
                var outputPath = Path.Combine(workingDir.FullName, "output");
                Directory.CreateDirectory(outputPath);

                var packageA = Util.CreatePackage(repositoryPath, "packageA", "1.0.0");

                // Server setup
                var indexJson = Util.CreateIndexJson();

                Util.AddFlatContainerResource(indexJson, server);
                Util.AddRegistrationResource(indexJson, server);
                var hitsByUrl = new ConcurrentDictionary<string, int>();

                server.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl, server, indexJson, localRepo);
                });

                server.Start();
                var sources = new List<string>() { server.Uri + "index.json" };
                Util.CreateNuGetConfig(workingPath, sources);

                string[] args = new string[] {
                    "install",
                    "packageA",
                    "-OutputDirectory",
                    outputPath,
                    "-Verbosity",
                    "detailed",
                    "-DisableParallelProcessing"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
                Assert.Equal(1, hitsByUrl["/index.json"]);
                Assert.Equal(1, hitsByUrl["/reg/packagea/index.json"]);

                foreach (var url in hitsByUrl.Keys)
                {
                    Assert.True(1 == hitsByUrl[url], url);
                }
            }
        }

        [Fact]
        public void NetworkCallCount_RestoreSolutionMultipleSourcesV2V3()
        {
            // Arrange
            using (var server2 = new MockServer())
            using (var server = new MockServer())
            {
                var workingDir = CreateMixedConfigAndJson();
                var workingPath = workingDir.FullName;
                var repositoryPath = Path.Combine(workingPath, "repo");
                var slnPath = Path.Combine(workingPath, "test.sln");
                var nugetexe = Util.GetNuGetExePath();
                var localRepo = new LocalPackageRepository(repositoryPath);

                // Server setup
                var indexJson = Util.CreateIndexJson();

                Util.AddFlatContainerResource(indexJson, server);
                Util.AddRegistrationResource(indexJson, server);
                var hitsByUrl = new ConcurrentDictionary<string, int>();
                var hitsByUrl2 = new ConcurrentDictionary<string, int>();

                server.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl, server, indexJson, localRepo);
                });

                server2.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl2, server2, indexJson, localRepo);
                });

                server.Start();
                server2.Start();
                var sources = new List<string>() { server.Uri + "index.json", server2.Uri + "nuget" };
                Util.CreateNuGetConfig(workingPath, sources);

                string[] args = new string[] {
                    "restore",
                    slnPath,
                    "-Verbosity",
                    "detailed",
                    "-DisableParallelProcessing"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                Assert.Equal(1, hitsByUrl["/index.json"]);
                Assert.Equal(1, hitsByUrl2["/nuget"]);
            }
        }

        [Fact]
        public void NetworkCallCount_RestoreSolutionMultipleSourcesTwoV2()
        {
            // Arrange
            using (var server2 = new MockServer())
            using (var server = new MockServer())
            {
                var workingDir = CreateMixedConfigAndJson();
                var workingPath = workingDir.FullName;
                var repositoryPath = Path.Combine(workingPath, "repo");
                var slnPath = Path.Combine(workingPath, "test.sln");
                var nugetexe = Util.GetNuGetExePath();
                var localRepo = new LocalPackageRepository(repositoryPath);

                // Server setup
                var indexJson = Util.CreateIndexJson();

                Util.AddFlatContainerResource(indexJson, server);
                Util.AddRegistrationResource(indexJson, server);
                var hitsByUrl = new ConcurrentDictionary<string, int>();
                var hitsByUrl2 = new ConcurrentDictionary<string, int>();

                server.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl, server, indexJson, localRepo);
                });

                server2.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl2, server2, indexJson, localRepo);
                });

                server.Start();
                server2.Start();
                var sources = new List<string>() { server.Uri + "nuget", server2.Uri + "nuget" };
                Util.CreateNuGetConfig(workingPath, sources);

                string[] args = new string[] {
                    "restore",
                    slnPath,
                    "-Verbosity",
                    "detailed",
                    "-DisableParallelProcessing"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                foreach (var url in hitsByUrl.Keys)
                {
                    Assert.True(1 == hitsByUrl[url], url + " hits: " + hitsByUrl[url]);
                }

                foreach (var url in hitsByUrl2.Keys)
                {
                    Assert.True(1 == hitsByUrl2[url], url + " hits: " + hitsByUrl2[url]);
                }
            }
        }

        [Fact]
        public void NetworkCallCount_RestoreSolutionMultipleSourcesTwoV3()
        {
            // Arrange
            using (var server2 = new MockServer())
            using (var server = new MockServer())
            {
                var workingDir = CreateMixedConfigAndJson();
                var workingPath = workingDir.FullName;
                var repositoryPath = Path.Combine(workingPath, "repo");
                var slnPath = Path.Combine(workingPath, "test.sln");
                var nugetexe = Util.GetNuGetExePath();
                var localRepo = new LocalPackageRepository(repositoryPath);

                // Server setup
                var indexJson = Util.CreateIndexJson();
                var indexJson2 = Util.CreateIndexJson();

                Util.AddFlatContainerResource(indexJson, server);
                Util.AddRegistrationResource(indexJson, server);

                Util.AddFlatContainerResource(indexJson2, server2);
                Util.AddRegistrationResource(indexJson2, server2);

                var hitsByUrl = new ConcurrentDictionary<string, int>();
                var hitsByUrl2 = new ConcurrentDictionary<string, int>();

                server.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl, server, indexJson, localRepo);
                });

                server2.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl2, server2, indexJson2, localRepo);
                });

                server.Start();
                server2.Start();

                var sources = new List<string>() { server.Uri + "index.json", server2.Uri + "index.json" };
                Util.CreateNuGetConfig(workingPath, sources);

                string[] args = new string[] {
                    "restore",
                    slnPath,
                    "-Verbosity",
                    "detailed",
                    "-DisableParallelProcessing"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                foreach (var url in hitsByUrl.Keys)
                {
                    Assert.True(1 == hitsByUrl[url], url);
                }

                foreach (var url in hitsByUrl2.Keys)
                {
                    Assert.True(1 == hitsByUrl2[url], url);
                }
            }
        }

        [Fact]
        public void NetworkCallCount_RestoreSolutionV3WithoutFlatContainer()
        {
            // Arrange
            using (var server = new MockServer())
            {
                var workingDir = CreateMixedConfigAndJson();
                var workingPath = workingDir.FullName;
                var repositoryPath = Path.Combine(workingPath, "repo");
                var slnPath = Path.Combine(workingPath, "test.sln");
                var nugetexe = Util.GetNuGetExePath();
                var localRepo = new LocalPackageRepository(repositoryPath);

                // Server setup
                var indexJson = Util.CreateIndexJson();

                Util.AddRegistrationResource(indexJson, server);
                var hitsByUrl = new ConcurrentDictionary<string, int>();

                server.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl, server, indexJson, localRepo);
                });

                server.Start();
                var sources = new List<string>() { server.Uri + "index.json" };
                Util.CreateNuGetConfig(workingPath, sources);

                string[] args = new string[] {
                    "restore",
                    slnPath,
                    "-Verbosity",
                    "detailed",
                    "-DisableParallelProcessing"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
                Assert.Equal(1, hitsByUrl["/index.json"]);

                // PackageE is hit twice, once from packages.config and the other from project.json.
                // The rest should only be hit once.
                foreach (var url in hitsByUrl.Keys.Where(s => s != "/reg/packagee/index.json"))
                {
                    var hits = hitsByUrl[url];
                    Assert.True(1 == hits, url + $" was hit {hitsByUrl[url]} times instead of 1");
                }
            }
        }

        [Fact]
        public void NetworkCallCount_RestoreSolutionWithPackagesConfigAndProjectJsonV3()
        {
            // Arrange
            using (var server = new MockServer())
            {
                var workingDir = CreateMixedConfigAndJson();
                var workingPath = workingDir.FullName;
                var repositoryPath = Path.Combine(workingPath, "repo");
                var slnPath = Path.Combine(workingPath, "test.sln");
                var nugetexe = Util.GetNuGetExePath();
                var localRepo = new LocalPackageRepository(repositoryPath);

                // Server setup
                var indexJson = Util.CreateIndexJson();

                Util.AddFlatContainerResource(indexJson, server);
                Util.AddRegistrationResource(indexJson, server);
                var hitsByUrl = new ConcurrentDictionary<string, int>();

                server.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl, server, indexJson, localRepo);
                });

                server.Start();
                var sources = new List<string>() { server.Uri + "index.json" };
                Util.CreateNuGetConfig(workingPath, sources);

                string[] args = new string[] {
                    "restore",
                    slnPath,
                    "-Verbosity",
                    "detailed",
                    "-DisableParallelProcessing"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                server.Stop();

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
                Assert.Equal(1, hitsByUrl["/index.json"]);

                foreach (var url in hitsByUrl.Keys)
                {
                    Assert.True(1 == hitsByUrl[url], url);
                }
            }
        }

        [Fact]
        public void NetworkCallCount_RestoreSolutionWithPackagesConfigAndProjectJsonV2()
        {
            // Arrange
            using (var server = new MockServer())
            {
                var workingDir = CreateMixedConfigAndJson();
                var workingPath = workingDir.FullName;
                var repositoryPath = Path.Combine(workingPath, "repo");
                var slnPath = Path.Combine(workingPath, "test.sln");
                var nugetexe = Util.GetNuGetExePath();
                var localRepo = new LocalPackageRepository(repositoryPath);

                // Server setup
                var indexJson = Util.CreateIndexJson();

                Util.AddFlatContainerResource(indexJson, server);
                Util.AddRegistrationResource(indexJson, server);
                Util.AddFlatContainerResource(indexJson, server);
                var hitsByUrl = new ConcurrentDictionary<string, int>();

                server.Get.Add("/", request =>
                {
                    return ServerHandler(request, hitsByUrl, server, indexJson, localRepo);
                });

                server.Start();
                var sources = new List<string>() { server.Uri + "nuget" };
                Util.CreateNuGetConfig(workingPath, sources);

                string[] args = new string[] {
                    "restore",
                    slnPath,
                    "-Verbosity",
                    "detailed",
                    "-DisableParallelProcessing"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);
                Assert.Equal(1, hitsByUrl["/nuget"]);

                foreach (var url in hitsByUrl.Keys)
                {
                    Assert.True(1 == hitsByUrl[url], url);
                }
            }
        }

        private DirectoryInfo CreateMixedConfigAndJson()
        {
            var tempPath = Path.GetTempPath();
            var guid = Guid.NewGuid();
            var workingPath = Path.Combine(tempPath, guid.ToString());
            var repositoryPath = Path.Combine(workingPath, "repo");
            var currentDirectory = Directory.GetCurrentDirectory();
            var proj1Dir = Path.Combine(workingPath, "proj1");
            var proj2Dir = Path.Combine(workingPath, "proj2");
            var proj3Dir = Path.Combine(workingPath, "proj3");
            var proj4Dir = Path.Combine(workingPath, "proj4");

            _dirs.TryAdd(workingPath, false);

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(proj1Dir);
            Util.CreateDirectory(proj2Dir);
            Util.CreateDirectory(proj3Dir);
            Util.CreateDirectory(proj4Dir);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));

            var packageA = Util.CreatePackage(repositoryPath, "packageA", "1.0.0");
            var packageB = Util.CreatePackage(repositoryPath, "packageB", "1.0.0");
            var packageC = Util.CreatePackage(repositoryPath, "packageC", "1.0.0");

            var packageD = Util.CreatePackage(repositoryPath, "packageD", "1.0.0");
            var packageE = Util.CreatePackage(repositoryPath, "packageE", "1.0.0");
            var packageF = Util.CreatePackage(repositoryPath, "packageF", "1.0.0");

            Util.CreateFile(
               proj1Dir,
               "packages.config",
               @"<packages>
                     <package id=""packageA"" version=""1.0.0"" />
                     <package id=""packageB"" version=""1.0.0"" />
                  </packages>");

            Util.CreateFile(
               proj2Dir,
               "packages.config",
               @"<packages>
                     <package id=""packageC"" version=""1.0.0"" />
                     <package id=""packageB"" version=""1.0.0"" />
                  </packages>");

            Util.CreateFile(proj3Dir, "project.json",
                                            @"{
                                            'dependencies': {
                                                'packageD': '1.0.0',
                                                'packageE': '1.0.*'
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            Util.CreateFile(proj4Dir, "project.json",
                                            @"{
                                            'dependencies': {
                                                'packageE': '1.0.0',
                                                'packageF': '*'
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            var projectContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
                                <Project ToolsVersion=""14.0"" DefaultTargets=""Build""
                                xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                <Target Name=""_NuGet_GetProjectsReferencingProjectJsonInternal""></Target>
                </Project>";

            Util.CreateFile(proj1Dir, "proj1.csproj", projectContent);
            Util.CreateFile(proj2Dir, "proj2.csproj", projectContent);
            Util.CreateFile(proj3Dir, "proj3.csproj", projectContent);
            Util.CreateFile(proj4Dir, "proj4.csproj", projectContent);

            var slnPath = Path.Combine(workingPath, "test.sln");

            Util.CreateFile(workingPath, "test.sln",
                       @"
                        Microsoft Visual Studio Solution File, Format Version 12.00
                        # Visual Studio 14
                        VisualStudioVersion = 14.0.23107.0
                        MinimumVisualStudioVersion = 10.0.40219.1
                        Project(""{AAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj1"", ""proj1\proj1.csproj"", ""{AA6279C1-B5EE-4C6B-9FA3-A794CE195136}""
                        EndProject
                        Project(""{BBE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj2"", ""proj2\proj2.csproj"", ""{BB6279C1-B5EE-4C6B-9FA3-A794CE195136}""
                        EndProject
                        Project(""{CCE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj3"", ""proj3\proj3.csproj"", ""{CC6279C1-B5EE-4C6B-9FA3-A794CE195136}""
                        EndProject
                        Project(""{DDE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj4"", ""proj4\proj4.csproj"", ""{DD6279C1-B5EE-4C6B-9FA3-A794CE195136}""
                        EndProject
                        Global
                            GlobalSection(SolutionConfigurationPlatforms) = preSolution
                                Debug|Any CPU = Debug|Any CPU
                                Release|Any CPU = Release|Any CPU
                            EndGlobalSection
                            GlobalSection(ProjectConfigurationPlatforms) = postSolution
                                {AA6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                                {AA6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.Build.0 = Debug|Any CPU
                                {BB6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                                {BB6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.Build.0 = Debug|Any CPU
                                {CC6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                                {CC6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.Build.0 = Debug|Any CPU
                                {DD6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                                {DD6279C1-B5EE-4C6B-9FA3-A794CE195136}.Debug|Any CPU.Build.0 = Debug|Any CPU
                            EndGlobalSection
                            GlobalSection(SolutionProperties) = preSolution
                                HideSolutionNode = FALSE
                            EndGlobalSection
                        EndGlobal
                        ");

            return new DirectoryInfo(workingPath);
        }

        private Action<HttpListenerResponse> ServerHandler(
            HttpListenerRequest request,
            ConcurrentDictionary<string, int> hitsByUrl,
            MockServer server,
            JObject indexJson,
            LocalPackageRepository localRepo)
        {
            return ServerHandler(request,
                hitsByUrl,
                server,
                indexJson,
                localRepo,
                new ManualResetEventSlim(true),
                new ManualResetEventSlim(true));
        }

        private Action<HttpListenerResponse> ServerHandler(
            HttpListenerRequest request,
            ConcurrentDictionary<string, int> hitsByUrl,
            MockServer server,
            JObject indexJson,
            LocalPackageRepository localRepo,
            ManualResetEventSlim v2DownloadWait,
            ManualResetEventSlim v3DownloadWait)
        {
            try
            {
                var path = request.Url.AbsolutePath;
                var parts = request.Url.AbsolutePath.Split('/');
                var repositoryPath = localRepo.Source;

                // track hits on the url
                var urlHits = hitsByUrl.AddOrUpdate(request.RawUrl, 1, (s, i) => i + 1);

                if (path == "/index.json")
                {
                    return new Action<HttpListenerResponse>(response =>
                    {
                        response.StatusCode = 200;
                        response.ContentType = "text/javascript";
                        MockServer.SetResponseContent(response, indexJson.ToString());
                    });
                }
                else if (path.StartsWith("/nuget/Packages(Id="))
                {
                    var splitOnSingleQuote = path.Split('\'');

                    var id = splitOnSingleQuote.Skip(1).First();
                    var version = splitOnSingleQuote.Skip(3).First();

                    var file = new FileInfo(Path.Combine(repositoryPath, $"{id}.{version}.nupkg"));

                    if (file.Exists)
                    {
                        var package = new ZipPackage(file.FullName);

                        return new Action<HttpListenerResponse>(response =>
                        {
                            response.ContentType = "application/atom+xml;type=entry;charset=utf-8";
                            var odata = server.ToOData(package);
                            MockServer.SetResponseContent(response, odata);
                        });
                    }
                    else
                    {
                        return new Action<HttpListenerResponse>(response =>
                        {
                            response.StatusCode = 404;
                        });
                    }
                }
                else if (path.StartsWith("/packages/"))
                {
                    v3DownloadWait.Wait();

                    var file = new FileInfo(Path.Combine(repositoryPath, parts.Last()));

                    if (file.Exists)
                    {
                        return new Action<HttpListenerResponse>(response =>
                        {
                            response.ContentType = "application/zip";
                            using (var stream = file.OpenRead())
                            {
                                var content = stream.ReadAllBytes();
                                MockServer.SetResponseContent(response, content);
                            }
                        });
                    }
                    else
                    {
                        return new Action<HttpListenerResponse>(response =>
                        {
                            response.StatusCode = 404;
                        });
                    }
                }
                else if (path.StartsWith("/package/") || path.StartsWith("/nuget/package/"))
                {
                    v2DownloadWait.Wait();

                    var id = parts.Reverse().Skip(1).First();
                    var version = parts.Last();

                    var file = new FileInfo(Path.Combine(repositoryPath, $"{id}.{version}.nupkg"));

                    if (file.Exists)
                    {
                        return new Action<HttpListenerResponse>(response =>
                        {
                            response.ContentType = "application/zip";
                            using (var stream = file.OpenRead())
                            {
                                var content = stream.ReadAllBytes();
                                MockServer.SetResponseContent(response, content);
                            }
                        });
                    }
                    else
                    {
                        return new Action<HttpListenerResponse>(response =>
                        {
                            response.StatusCode = 404;
                        });
                    }
                }
                else if (path.StartsWith("/reg/") && path.EndsWith("/index.json"))
                {
                    var id = parts.Reverse().Skip(1).First();
                    var version = "1.0.0";

                    return new Action<HttpListenerResponse>(response =>
                    {
                        response.ContentType = "text/javascript";

                        var regBlob = Util.CreateSinglePackageRegistrationBlob(server, id, version);

                        MockServer.SetResponseContent(response, regBlob.ToString());
                    });
                }
                else if (path.StartsWith("/flat/") && path.EndsWith("/index.json"))
                {
                    return new Action<HttpListenerResponse>(response =>
                    {
                        response.ContentType = "text/javascript";

                        MockServer.SetResponseContent(response, @"{
                              ""versions"": [
                                ""1.0.0""
                              ]
                            }");
                    });
                }
                else if (path.StartsWith("/flat/") && path.EndsWith(".nupkg"))
                {
                    v3DownloadWait.Wait();

                    var file = new FileInfo(Path.Combine(repositoryPath, parts.Last()));

                    if (file.Exists)
                    {
                        return new Action<HttpListenerResponse>(response =>
                        {
                            response.ContentType = "application/zip";
                            using (var stream = file.OpenRead())
                            {
                                var content = stream.ReadAllBytes();
                                MockServer.SetResponseContent(response, content);
                            }
                        });
                    }
                    else
                    {
                        return new Action<HttpListenerResponse>(response =>
                        {
                            response.StatusCode = 404;
                        });
                    }
                }
                else if (path.StartsWith("/nuget/FindPackagesById()"))
                {
                    var id = request.QueryString.Get("id").Trim('\'');
                    var package = localRepo.FindPackagesById(id).Single();

                    return new Action<HttpListenerResponse>(response =>
                    {
                        response.ContentType = "application/atom+xml;type=feed;charset=utf-8";
                        var feed = server.ToODataFeed(new[] { package }, "FindPackagesById");
                        MockServer.SetResponseContent(response, feed);
                    });
                }
                else if (path == "/nuget/$metadata")
                {
                    return new Action<HttpListenerResponse>(response =>
                    {
                        MockServer.SetResponseContent(response, MockServerResource.NuGetV2APIMetadata);
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
            }
            catch (Exception)
            {
                // Debug here
                throw;
            }
        }

        private static int GetMachineCacheCount()
        {
            if (Directory.Exists(MachineCache.Default.Source))
            {
                return Directory.GetFiles(MachineCache.Default.Source).Count();
            }

            return 0;
        }

        /// <summary>
        /// Fully delete the machine cache including temp files
        /// </summary>
        private static void ClearMachineCache()
        {
            var dir = MachineCache.Default.Source;

            if (Directory.Exists(dir))
            {
                foreach (var file in Directory.GetFiles(MachineCache.Default.Source))
                {
                    File.Delete(file);
                }
            }
        }

        /// <summary>
        /// Store all directories used by the unit tests and clean them up at the end during Dispose()
        /// </summary>
        private ConcurrentDictionary<string, bool> _dirs = new ConcurrentDictionary<string, bool>();

        public void Dispose()
        {
            foreach (var dir in _dirs.Keys)
            {
                try
                {
                    Util.DeleteDirectory(dir);
                }
                catch
                {
                    // Ignore failures
                    Console.WriteLine("Unable to delete: " + dir);
                }
            }
        }

        public NetworkCallCountTest()
        {
            // Clear the machine cache before each test
            ClearMachineCache();
        }
    }
}
