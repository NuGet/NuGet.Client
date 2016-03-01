using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Protocol.Core.v3;
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

                var restoreContext = new RestoreArgs()
                {
                    CacheContext = new SourceCacheContext(),
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
}
