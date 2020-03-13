// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Xunit;
using NuGet.Commands;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using FluentAssertions.Common;

namespace NuGet.Commands.Test
{
    public class RestoreResultTests
    {
        [Fact]
        public async Task RestoreResult_WritesCommitToInformation()
        {
            // Arrange
            using (var td = TestDirectory.Create())
            {
                var path = Path.Combine(td, "project.lock.json");
                var logger = new TestLogger();
                var result = new RestoreResult(
                    success: true,
                    restoreGraphs: null,
                    compatibilityCheckResults: null,
                    lockFile: new LockFile(),
                    previousLockFile: null, // different lock file
                    lockFilePath: path,
                    msbuildFiles: Enumerable.Empty<MSBuildOutputFile>(),
                    cacheFile: null,
                    cacheFilePath: null,
                    packagesLockFilePath: null,
                    packagesLockFile: null,
                    dependencyGraphSpecFilePath: null,
                    dependencyGraphSpec: null,
                    projectStyle: ProjectStyle.Unknown,
                    elapsedTime: TimeSpan.MinValue);

                // Act
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.Empty(logger.MinimalMessages);
                Assert.Contains(
                    $"Writing assets file to disk. Path: {path}",
                    logger.Messages);
                Assert.True(File.Exists(path), $"The lock file should have been written: {path}");
                Assert.Equal(1, logger.Messages.Count);
            }
        }

        [Fact]
        public async Task RestoreResult_WritesSkipCommitToInformation()
        {
            // Arrange
            using (var td = TestDirectory.Create())
            {
                var path = Path.Combine(td, "project.lock.json");
                var logger = new TestLogger();
                var result = new RestoreResult(
                    success: true,
                    restoreGraphs: null,
                    compatibilityCheckResults: null,
                    lockFile: new LockFile(),
                    previousLockFile: new LockFile(), // same lock file
                    lockFilePath: path,
                    msbuildFiles: Enumerable.Empty<MSBuildOutputFile>(),
                    cacheFile: null,
                    cacheFilePath: null,
                    packagesLockFilePath: null,
                    packagesLockFile: null,
                    dependencyGraphSpecFilePath: null,
                    dependencyGraphSpec: null,
                    projectStyle: ProjectStyle.Unknown,
                    elapsedTime: TimeSpan.MinValue);

                // Act
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.Contains(
                    $"Assets file has not changed. Skipping assets file writing. Path: {path}",
                    logger.Messages);
                Assert.False(File.Exists(path), $"The lock file should not have been written: {path}");
                Assert.Equal(1, logger.Messages.Count);
            }
        }

        [Fact]
        public async Task RestoreResult_WritesCommitToInformation_AssetsAndCache()
        {
            // Arrange
            using (var td = TestDirectory.Create())
            {
                var path = Path.Combine(td, "project.lock.json");
                var cachePath = Path.Combine(td, "project.csproj.nuget.cache");
                var logger = new TestLogger();
                var result = new RestoreResult(
                    success: true,
                    restoreGraphs: null,
                    compatibilityCheckResults: null,
                    lockFile: new LockFile(),
                    previousLockFile: null, // different lock file
                    lockFilePath: path,
                    msbuildFiles: Enumerable.Empty<MSBuildOutputFile>(),
                    cacheFile: new CacheFile("NotSoRandomString"),
                    cacheFilePath: cachePath,
                    packagesLockFilePath: null,
                    packagesLockFile: null,
                    dependencyGraphSpecFilePath: null,
                    dependencyGraphSpec: null,
                    projectStyle: ProjectStyle.Unknown,
                    elapsedTime: TimeSpan.MinValue);

                // Act
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.Empty(logger.MinimalMessages);
                Assert.Contains(
                    $"Writing assets file to disk. Path: {path}",
                    logger.Messages);
                Assert.Contains(
                    $"Writing cache file to disk. Path: {cachePath}",
                    logger.VerboseMessages);

                Assert.True(File.Exists(path), $"The lock file should have been written: {path}");
                Assert.True(File.Exists(cachePath), $"The cache file should have been written: {cachePath}");
                Assert.Equal(2, logger.Messages.Count);
            }
        }


        [Fact]
        public async Task NoOpRestoreResultTest_SkipsFileWriting()
        {
            // Arrange
            using (var td = TestDirectory.Create())
            {
                var path = Path.Combine(td, "project.lock.json");
                var cachePath = Path.Combine(td, "project.csproj.nuget.cache");
                var logger = new TestLogger();
                var result = new NoOpRestoreResult(
                    success: true,
                    lockFilePath: path,
                    new Lazy<LockFile>(() => new LockFile()), 
                    cacheFile: new CacheFile("NotSoRandomString"),
                    cacheFilePath: cachePath,
                    projectStyle: ProjectStyle.Unknown,
                    elapsedTime: TimeSpan.MinValue);

                // Act
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.Contains(
                    $"Assets file has not changed. Skipping assets file writing. Path: {path}",
                    logger.Messages);
                Assert.Contains(
                    $"No-Op restore. The cache will not be updated. Path: {cachePath}",
                    logger.VerboseMessages);

                Assert.False(File.Exists(path), $"The lock file should not have been written: {path}");
                Assert.False(File.Exists(cachePath), $"The cache file should not have been written: {cachePath}");
                Assert.Equal(2, logger.Messages.Count);
            }
        }

        [Fact]
        public void NoOpRestoreResult_IsLazy()
        {
            const string lockFileContent = @"{
  ""version"": 1,
  ""targets"": {
    "".NETPlatform,Version=v5.0"": {
      ""System.Runtime/4.0.10-beta-23008"": {
        ""compile"": {
          ""ref/dotnet/System.Runtime.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""System.Runtime/4.0.10-beta-23008"": {
      ""sha512"": ""JkGp8sCzxxRY1GS+p1SEk8WcaT8pu++/5b94ar2i/RaUN/OzkcGP/6OLFUxUf1uar75pUvotpiMawVt1dCEUVA=="",
      ""type"": ""Package"",
      ""files"": [
        ""_rels/.rels"",
        ""System.Runtime.nuspec"",
        ""License.rtf"",
        ""ref/dotnet/System.Runtime.dll"",
        ""ref/net451/_._"",
        ""lib/net451/_._"",
        ""ref/win81/_._"",
        ""lib/win81/_._"",
        ""ref/netcore50/System.Runtime.dll"",
        ""package/services/metadata/core-properties/cdec43993f064447a2d882cbfd022539.psmdcp"",
        ""[Content_Types].xml""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    """": [
      ""System.Runtime >= 4.0.10-beta-*""
    ],
    "".NETPlatform,Version=v5.0"": []
  }
}
";

            // Arrange
            using (var td = TestDirectory.Create())
            {
                var path = Path.Combine(td, "project.lock.json");
                File.WriteAllText(path, lockFileContent);
                var logger = new TestLogger();
                var result = new NoOpRestoreResult(
                    success: true,
                    lockFilePath: path,
                    new Lazy<LockFile>(() => LockFileUtilities.GetLockFile(path, logger)),
                    cacheFile: new CacheFile("NotSoRandomString"),
                    cacheFilePath: null,
                    projectStyle: ProjectStyle.Unknown,
                    elapsedTime: TimeSpan.MinValue);

                // Act
                var actual = result.LockFile;

                // Assert
                Assert.Equal(1, actual.Libraries.Count);
                Assert.Equal("System.Runtime", actual.Libraries[0].Name);
            }
        }

        [Fact]
        public async Task RestoreResult_Commit_WritesDependencyGraphSpec()
        {
            // Arrange
            using (var td = TestDirectory.Create())
            {
                var path = Path.Combine(td, "project.assets.json");
                var cachePath = Path.Combine(td, "project.csproj.nuget.cache");
                var dgSpecPath = Path.Combine(td, "project1.nuget.g.dgspec.json");
                var dgSpec = new DependencyGraphSpec();
                var configJson = @"
                {
                    ""dependencies"": {
                    },
                     ""frameworks"": {
                        ""net45"": { }
                    }
                }";

                var spec = JsonPackageSpecReader.GetPackageSpec(configJson, "TestProject", Path.Combine(td, "project.csproj")).WithTestRestoreMetadata();
                dgSpec.AddProject(spec);
                dgSpec.AddRestore(spec.Name);

                var logger = new TestLogger();
                var result = new RestoreResult(
                    success: true,
                    restoreGraphs: null,
                    compatibilityCheckResults: null,
                    lockFile: new LockFile(),
                    previousLockFile: null, // different lock file
                    lockFilePath: path,
                    msbuildFiles: Enumerable.Empty<MSBuildOutputFile>(),
                    cacheFile: new CacheFile("NotSoRandomString"),
                    cacheFilePath: cachePath,
                    packagesLockFilePath: null,
                    packagesLockFile: null,
                    dependencyGraphSpecFilePath: dgSpecPath,
                    dependencyGraphSpec: dgSpec,
                    projectStyle: ProjectStyle.Unknown,
                    elapsedTime: TimeSpan.MinValue);

                // Act
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.Empty(logger.MinimalMessages);
                Assert.Contains(
                    $"Persisting dg to {dgSpecPath}",
                    logger.VerboseMessages);
                Assert.True(File.Exists(dgSpecPath));
            }
        }
    }
}
