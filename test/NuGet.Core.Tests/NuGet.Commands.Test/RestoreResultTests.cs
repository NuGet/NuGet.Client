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

namespace NuGet.Commands.Test
{
    public class RestoreResultTests
    {
        [Fact]
        public async Task RestoreResult_WritesCommitToMinimal()
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
                    projectStyle: ProjectStyle.Unknown,
                    elapsedTime: TimeSpan.MinValue);

                // Act
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.Contains(
                    $"Writing lock file to disk. Path: {path}",
                    logger.MinimalMessages);
                Assert.True(File.Exists(path), $"The lock file should have been written: {path}");
                Assert.Equal(1, logger.Messages.Count);
            }
        }

        [Fact]
        public async Task RestoreResult_WritesSkipCommitToMinimal()
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
                    projectStyle: ProjectStyle.Unknown,
                    elapsedTime: TimeSpan.MinValue);

                // Act
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.Contains(
                    $"Lock file has not changed. Skipping lock file write. Path: {path}",
                    logger.MinimalMessages);
                Assert.False(File.Exists(path), $"The lock file should not have been written: {path}");
                Assert.Equal(1, logger.Messages.Count);
            }
        }

        [Fact]
        public async Task RestoreResult_WritesCommitToMinimalAssetsAndCache()
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
                    projectStyle: ProjectStyle.Unknown,
                    elapsedTime: TimeSpan.MinValue);

                // Act
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.Contains(
                    $"Writing lock file to disk. Path: {path}",
                    logger.MinimalMessages);
                Assert.Contains(
                    $"Writing cache file to disk. Path: {cachePath}",
                    logger.MinimalMessages);

                Assert.True(File.Exists(path), $"The lock file should have been written: {path}");
                Assert.True(File.Exists(cachePath), $"The cache file should have been written: {cachePath}");
                Assert.Equal(2, logger.Messages.Count);
            }
        }
    }
}
