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
            using (var td = TestFileSystemUtility.CreateRandomTestFolder())
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
                    msbuild: new MSBuildRestoreResult("project", td, true),
                    toolRestoreResults: Enumerable.Empty<ToolRestoreResult>());
                
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
            using (var td = TestFileSystemUtility.CreateRandomTestFolder())
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
                    msbuild: new MSBuildRestoreResult("project", td, true),
                    toolRestoreResults: Enumerable.Empty<ToolRestoreResult>());
                
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
        public async Task RestoreResult_WritesToolCommitToDebug()
        {
            // Arrange
            using (var td = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var path = Path.Combine(td, ".tools", "project.lock.json");
                var logger = new TestLogger();
                var toolResult = new ToolRestoreResult(
                    toolName: null,
                    success: true,
                    graphs: null,
                    lockFileTarget: null,
                    fileTargetLibrary: null,
                    lockFilePath: path,
                    lockFile: new LockFile(),
                    previousLockFile: null);
                var result = new RestoreResult(
                    success: true,
                    restoreGraphs: null,
                    compatibilityCheckResults: null,
                    lockFile: new LockFile(),
                    previousLockFile: new LockFile(), // same lock file
                    lockFilePath: null,
                    msbuild: new MSBuildRestoreResult("project", td, true),
                    toolRestoreResults: new[] { toolResult });
                
                // Act
                await result.CommitAsync(logger, CancellationToken.None);
                
                // Assert
                Assert.Contains(
                    $"Writing tool lock file to disk. Path: {path}",
                    logger.DebugMessages);
                Assert.True(File.Exists(path), $"The tool lock file should have been written: {path}");
                Assert.Equal(1, logger.DebugMessages.Count);
            }
        }
        
        [Fact]
        public async Task RestoreResult_WritesToolSkipCommitToDebug()
        {
            // Arrange
            using (var td = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var path = Path.Combine(td, "tools", "project.lock.json");
                var logger = new TestLogger();
                var toolResult = new ToolRestoreResult(
                    toolName: null,
                    success: true,
                    graphs: null,
                    lockFileTarget: null,
                    fileTargetLibrary: null,
                    lockFilePath: path,
                    lockFile: new LockFile(),
                    previousLockFile: new LockFile()); // same lock file
                var result = new RestoreResult(
                    success: true,
                    restoreGraphs: null,
                    compatibilityCheckResults: null,
                    lockFile: new LockFile(),
                    previousLockFile: new LockFile(), // same lock file
                    lockFilePath: null,
                    msbuild: new MSBuildRestoreResult("project", td, true),
                    toolRestoreResults: new[] { toolResult });
                
                // Act
                await result.CommitAsync(logger, CancellationToken.None);
                
                // Assert
                Assert.Contains(
                    $"Tool lock file has not changed. Skipping lock file write. Path: {path}",
                    logger.DebugMessages);
                Assert.False(File.Exists(path), $"The tool lock file should not have been written: {path}");
                Assert.Equal(1, logger.DebugMessages.Count);
            }
        }
    }
}
