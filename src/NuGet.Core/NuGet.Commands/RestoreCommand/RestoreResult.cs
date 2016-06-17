// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.LibraryModel;
using NuGet.ProjectModel;

namespace NuGet.Commands
{
    public class RestoreResult : IRestoreResult
    {
        public bool Success { get; }

        public MSBuildRestoreResult MSBuild { get; }

        /// <summary>
        /// Gets the path that the lock file will be written to.
        /// </summary>
        public string LockFilePath { get; set; }

        /// <summary>
        /// Gets the resolved dependency graphs produced by the restore operation
        /// </summary>
        public IEnumerable<RestoreTargetGraph> RestoreGraphs { get; }

        public IEnumerable<CompatibilityCheckResult> CompatibilityCheckResults { get; }

        public IEnumerable<ToolRestoreResult> ToolRestoreResults { get; }

        /// <summary>
        /// Gets a boolean indicating if the lock file will be re-written on <see cref="Commit"/>
        /// because the file needs to be re-locked.
        /// </summary>
        public bool RelockFile { get; }

        /// <summary>
        /// Gets the lock file that was generated during the restore or, in the case of a locked lock file,
        /// was used to determine the packages to install during the restore.
        /// </summary>
        public LockFile LockFile { get; }

        /// <summary>
        /// The existing lock file. This is null if no lock file was provided on the <see cref="RestoreRequest"/>.
        /// </summary>
        public LockFile PreviousLockFile { get; }

        public RestoreResult(
            bool success,
            IEnumerable<RestoreTargetGraph> restoreGraphs,
            IEnumerable<CompatibilityCheckResult> compatibilityCheckResults,
            LockFile lockFile,
            LockFile previousLockFile,
            string lockFilePath,
            MSBuildRestoreResult msbuild,
            IEnumerable<ToolRestoreResult> toolRestoreResults)
        {
            Success = success;
            RestoreGraphs = restoreGraphs;
            CompatibilityCheckResults = compatibilityCheckResults;
            LockFile = lockFile;
            LockFilePath = lockFilePath;
            MSBuild = msbuild;
            PreviousLockFile = previousLockFile;
            ToolRestoreResults = toolRestoreResults;
        }

        /// <summary>
        /// Calculates the complete set of all packages installed by this operation
        /// </summary>
        /// <remarks>
        /// This requires quite a bit of iterating over the graph so the result should be cached
        /// </remarks>
        /// <returns>A set of libraries that were installed by this operation</returns>
        public ISet<LibraryIdentity> GetAllInstalled()
        {
            return new HashSet<LibraryIdentity>(RestoreGraphs.Where(g => !g.InConflict).SelectMany(g => g.Install).Distinct().Select(m => m.Library));
        }

        /// <summary>
        /// Calculates the complete set of all unresolved dependencies for this operation
        /// </summary>
        /// <remarks>
        /// This requires quite a bit of iterating over the graph so the result should be cached
        /// </remarks>
        /// <returns>A set of dependencies that were unable to be resolved by this operation</returns>
        public ISet<LibraryRange> GetAllUnresolved()
        {
            return new HashSet<LibraryRange>(RestoreGraphs.SelectMany(g => g.Unresolved).Distinct());
        }

        /// <summary>
        /// Commits the lock file contained in <see cref="LockFile"/> and the MSBuild targets/props to
        /// the local file system.
        /// </summary>
        /// <remarks>If <see cref="PreviousLockFile"/> and <see cref="LockFile"/> are identical
        ///  the file will not be written to disk.</remarks>
        public async Task CommitAsync(ILogger log, CancellationToken token)
        {
            await CommitAsync(log, forceWrite: false, token: token);
        }

        /// <summary>
        /// Commits the lock file contained in <see cref="LockFile"/> and the MSBuild targets/props to
        /// the local file system.
        /// </summary>
        /// <remarks>If <see cref="PreviousLockFile"/> and <see cref="LockFile"/> are identical
        ///  the file will not be written to disk.</remarks>
        /// <param name="forceWrite">Write out the lock file even if no changes exist.</param>
        public async Task CommitAsync(ILogger log, bool forceWrite, CancellationToken token)
        {
            // Write the lock file
            var lockFileFormat = new LockFileFormat();

            await CommitAsync(
                lockFileFormat,
                result: this,
                log: log,
                forceWrite: forceWrite,
                toolCommit: false,
                token: token);

            foreach (var toolRestoreResult in ToolRestoreResults)
            {
                if (toolRestoreResult.LockFilePath != null && toolRestoreResult.LockFile != null)
                {
                    await CommitAsync(
                        lockFileFormat,
                        result: toolRestoreResult,
                        log: log,
                        forceWrite: forceWrite,
                        toolCommit: true,
                        token: token);
                }
            }

            MSBuild.Commit(log);
        }

        private static async Task CommitAsync(
            LockFileFormat lockFileFormat,
            IRestoreResult result,
            ILogger log,
            bool forceWrite,
            bool toolCommit,
            CancellationToken token)
        {
            // Don't write the lock file if it is Locked AND we're not re-locking the file
            if (!result.LockFile.IsLocked || result.RelockFile || toolCommit)
            {
                // Avoid writing out the lock file if it is the same to avoid triggering an intellisense
                // update on a restore with no actual changes.
                if (forceWrite
                    || result.PreviousLockFile == null
                    || !result.PreviousLockFile.Equals(result.LockFile))
                {
                    if (toolCommit)
                    {
                        log.LogDebug($"Writing tool lock file to disk. Path: {result.LockFilePath}");
                        
                        await ConcurrencyUtilities.ExecuteWithFileLockedAsync(
                            result.LockFilePath,
                            lockedToken =>
                            {
                                var lockFileDirectory = Path.GetDirectoryName(result.LockFilePath);
                                Directory.CreateDirectory(lockFileDirectory);
                                
                                lockFileFormat.Write(result.LockFilePath, result.LockFile);
                                
                                return Task.FromResult(0);
                            },
                            token);
                    }
                    else
                    {
                        log.LogMinimal($"Writing lock file to disk. Path: {result.LockFilePath}");
                        
                        lockFileFormat.Write(result.LockFilePath, result.LockFile);
                    }
                }
                else
                {
                    if (toolCommit)
                    {
                        log.LogDebug($"Tool lock file has not changed. Skipping lock file write. Path: {result.LockFilePath}");
                    }
                    else
                    {
                        log.LogMinimal($"Lock file has not changed. Skipping lock file write. Path: {result.LockFilePath}");
                    }
                }
            }
        }
        
        private static void WriteLockFile(
            LockFileFormat lockFileFormat,
            IRestoreResult result,
            bool createDirectory)
        {
            if (createDirectory)
            {
            }
            
            lockFileFormat.Write(result.LockFilePath, result.LockFile);
        }
    }
}
