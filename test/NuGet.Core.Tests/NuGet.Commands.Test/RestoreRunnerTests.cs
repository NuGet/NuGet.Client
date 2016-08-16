using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Commands.Test
{
    public class RestoreRunnerTests
    {
        [Fact]
        public async Task RestoreRunner_BasicRestore()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var project1Json = @"
            {
              ""version"": ""1.0.0"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);

                var logger = new TestLogger();
                var lockPath = Path.Combine(project1.FullName, "project.lock.json");

                var sourceRepos = sources.Select(source => Repository.Factory.GetCoreV3(source.Source)).ToList();

                var providerCache = new RestoreCommandProvidersCache();

                using (var cacheContext = new SourceCacheContext())
                {
                    var restoreContext = new RestoreArgs()
                    {
                        CacheContext = cacheContext,
                        DisableParallel = true,
                        GlobalPackagesFolder = packagesDir.FullName,
                        Sources = new List<string>() { packageSource.FullName },
                        Inputs = new List<string>() { specPath1 },
                        Log = logger,
                        CachingSourceProvider = new CachingSourceProvider(new TestPackageSourceProvider(sources)),
                        RequestProviders = new List<IRestoreRequestProvider>()
                        {
                            new ProjectJsonRestoreRequestProvider(providerCache)
                        }
                    };

                    // Act
                    var summaries = await RestoreRunner.Run(restoreContext);
                    var summary = summaries.Single();

                    // Assert
                    Assert.True(summary.Success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                    Assert.Equal(1, summary.FeedsUsed.Count);
                    Assert.True(File.Exists(lockPath), lockPath);
                }
            }
        }

        [Fact]
        public async Task RestoreRunner_BasicRestoreWithConfigFile()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var project1Json = @"
            {
              ""version"": ""1.0.0"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            var configFile = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""nuget.org"" value=""{0}"" />
    </packageSources>
</configuration>
";

            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);
                File.WriteAllText(Path.Combine(workingDir, "NuGet.Config"), String.Format(configFile, packageSource.FullName));

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);

                var configPath = Path.Combine(workingDir, "NuGet.Config");

                var logger = new TestLogger();
                var lockPath = Path.Combine(project1.FullName, "project.lock.json");

                var providerCache = new RestoreCommandProvidersCache();

                using (var cacheContext = new SourceCacheContext())
                {
                    var restoreContext = new RestoreArgs()
                    {
                        CacheContext = cacheContext,
                        DisableParallel = true,
                        GlobalPackagesFolder = packagesDir.FullName,
                        ConfigFile = configPath,
                        Inputs = new List<string>() { specPath1 },
                        Log = logger,
                        CachingSourceProvider = new CachingSourceProvider(new TestPackageSourceProvider(new List<PackageSource>())),
                        RequestProviders = new List<IRestoreRequestProvider>()
                        {
                            new ProjectJsonRestoreRequestProvider(providerCache)
                        }
                    };

                    // Act
                    var summaries = await RestoreRunner.Run(restoreContext);
                    var summary = summaries.Single();

                    // Assert
                    Assert.True(summary.Success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                    Assert.Equal(1, summary.FeedsUsed.Count);
                    Assert.True(File.Exists(lockPath), lockPath);
                }
            }
        }

        [Fact]
        public async Task RestoreRunner_RestoreWithExternalFile()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var project1Json = @"
            {
              ""version"": ""1.0.0"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            var project2Json = @"
            {
              ""version"": ""1.0.0"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                var project2 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project2"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                project2.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);
                File.WriteAllText(Path.Combine(project2.FullName, "project.json"), project1Json);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);

                var specPath2 = Path.Combine(project2.FullName, "project.json");
                var spec2 = JsonPackageSpecReader.GetPackageSpec(project2Json, "project2", specPath2);

                var projPath1 = Path.Combine(project1.FullName, "project1.csproj");
                var projPath2 = Path.Combine(project2.FullName, "project2.xproj");
                File.WriteAllText(projPath1, string.Empty);
                File.WriteAllText(projPath2, string.Empty);

                var logger = new TestLogger();
                var lockPath1 = Path.Combine(project1.FullName, "project.lock.json");
                var lockPath2 = Path.Combine(project2.FullName, "project.lock.json");

                var dgPath = Path.Combine(workingDir, "external.dg");

                var dgContent = new StringBuilder();
                dgContent.AppendLine($"#:{projPath1}");
                dgContent.AppendLine($"{projPath1}|{projPath2}");
                dgContent.AppendLine($"#:{projPath2}");

                File.WriteAllText(dgPath, dgContent.ToString());

                var sourceRepos = sources.Select(source => Repository.Factory.GetCoreV3(source.Source)).ToList();

                var providerCache = new RestoreCommandProvidersCache();

                using (var cacheContext = new SourceCacheContext())
                {
                    var restoreContext = new RestoreArgs()
                    {
                        CacheContext = cacheContext,
                        DisableParallel = true,
                        GlobalPackagesFolder = packagesDir.FullName,
                        Sources = new List<string>() { packageSource.FullName },
                        Inputs = new List<string>() { dgPath },
                        Log = logger,
                        CachingSourceProvider = new CachingSourceProvider(new TestPackageSourceProvider(sources)),
                        RequestProviders = new List<IRestoreRequestProvider>()
                        {
                            new MSBuildP2PRestoreRequestProvider(providerCache),
                            new ProjectJsonRestoreRequestProvider(providerCache)
                        }
                    };

                    // Act
                    var summaries = await RestoreRunner.Run(restoreContext);
                    var success = summaries.All(s => s.Success);

                    var lockFormat = new LockFileFormat();
                    var lockFile1 = lockFormat.Read(lockPath1);
                    var project2Lib = lockFile1.Libraries.First();

                    // Assert
                    Assert.True(success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                    Assert.True(File.Exists(lockPath1), lockPath1);
                    Assert.True(File.Exists(lockPath2), lockPath2);
                    Assert.Equal("project2", project2Lib.Name);
                }
            }
        }

        [Fact]
        public async Task RestoreRunner_RestoreFolder()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var project1Json = @"
            {
              ""version"": ""1.0.0"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            var project2Json = @"
            {
              ""version"": ""1.0.0"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                var project2 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project2"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                project2.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);
                File.WriteAllText(Path.Combine(project2.FullName, "project.json"), project1Json);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);

                var specPath2 = Path.Combine(project2.FullName, "project.json");
                var spec2 = JsonPackageSpecReader.GetPackageSpec(project2Json, "project2", specPath2);

                var logger = new TestLogger();
                var lockPath1 = Path.Combine(project1.FullName, "project.lock.json");
                var lockPath2 = Path.Combine(project2.FullName, "project.lock.json");

                var sourceRepos = sources.Select(source => Repository.Factory.GetCoreV3(source.Source)).ToList();

                var providerCache = new RestoreCommandProvidersCache();

                using (var cacheContext = new SourceCacheContext())
                {
                    var restoreContext = new RestoreArgs()
                    {
                        CacheContext = cacheContext,
                        DisableParallel = true,
                        GlobalPackagesFolder = packagesDir.FullName,
                        Sources = new List<string>() { packageSource.FullName },
                        Inputs = new List<string>() { workingDir },
                        Log = logger,
                        CachingSourceProvider = new CachingSourceProvider(new TestPackageSourceProvider(sources)),
                        RequestProviders = new List<IRestoreRequestProvider>()
                        {
                            new MSBuildP2PRestoreRequestProvider(providerCache),
                            new ProjectJsonRestoreRequestProvider(providerCache)
                        }
                    };

                    // Act
                    var summaries = await RestoreRunner.Run(restoreContext);
                    var success = summaries.All(s => s.Success);

                    // Assert
                    Assert.True(success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                    Assert.True(File.Exists(lockPath1), lockPath1);
                    Assert.True(File.Exists(lockPath2), lockPath2);
                }
            }
        }

        [Fact]
        public async Task RestoreRunner_RestoreWithRuntime()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var project1Json = @"
            {
              ""version"": ""1.0.0"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);

                var logger = new TestLogger();
                var lockPath1 = Path.Combine(project1.FullName, "project.lock.json");

                var sourceRepos = sources.Select(source => Repository.Factory.GetCoreV3(source.Source)).ToList();

                var providerCache = new RestoreCommandProvidersCache();

                using (var cacheContext = new SourceCacheContext())
                {
                    var restoreContext = new RestoreArgs()
                    {
                        CacheContext = cacheContext,
                        DisableParallel = true,
                        GlobalPackagesFolder = packagesDir.FullName,
                        Sources = new List<string>() { packageSource.FullName },
                        Inputs = new List<string>() { specPath1 },
                        Log = logger,
                        CachingSourceProvider = new CachingSourceProvider(new TestPackageSourceProvider(sources)),
                        RequestProviders = new List<IRestoreRequestProvider>()
                        {
                             new ProjectJsonRestoreRequestProvider(providerCache)
                        }
                    };

                    restoreContext.Runtimes.Add("linux-x86");

                    // Act
                    var summaries = await RestoreRunner.Run(restoreContext);
                    var success = summaries.All(s => s.Success);

                    var lockFormat = new LockFileFormat();
                    var lockFile = lockFormat.Read(lockPath1);

                    // Assert
                    Assert.True(success, "Failed: " + string.Join(Environment.NewLine, logger.Messages));
                    Assert.True(lockFile.Targets.Any(graph => graph.RuntimeIdentifier == "linux-x86"));
                }
            }
        }

        [Fact]
        public async Task RestoreRunner_WarnIfNoProject()
        {
            // If an input folder is provided to RestoreRunner that doesn't contain a project,
            // it should report an error.

            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var logger = new TestLogger();
                var providerCache = new RestoreCommandProvidersCache();
                using (var cacheContext = new SourceCacheContext())
                {
                    var restoreContext = new RestoreArgs()
                    {
                        CacheContext = cacheContext,
                        DisableParallel = true,
                        Inputs = new List<string>() { workingDir },
                        Log = logger,
                        RequestProviders = new List<IRestoreRequestProvider>()
                        {
                            new ProjectJsonRestoreRequestProvider(providerCache)
                        }
                    };

                    // Act
                    var summaries = await RestoreRunner.Run(restoreContext);

                    // Assert
                    Assert.Equal(0, summaries.Count);
                    var matchingError = logger.Messages.ToList().Find(error => error.Contains(workingDir));
                    Assert.NotNull(matchingError);
                }
            }
        }
    }
}
