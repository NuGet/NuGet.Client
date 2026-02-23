// Copyright (c) .NET Foundation. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace NuGet.Common
{
    /// <summary>
    /// Represents a manifest-based <see cref="EventSource" /> used for logging trace events from NuGet.
    /// </summary>
    [EventSource(Name = "Microsoft-NuGet")]
    public sealed class NuGetEventSource : EventSource
    {
        /// <summary>
        /// Gets the singleton instance of <see cref="NuGetEventSource" />.
        /// </summary>
        public static readonly NuGetEventSource Instance = new();

        private NuGetEventSource() { }

        /// <summary>
        /// Represents a class for declaring event keywords. Each keyword must be a flag (2^N) for use in a bitwise operation.
        /// </summary>
        public static class Keywords
        {
            /// <summary>
            /// The event keyword for tracing events related to NuGet's common functionality from the NuGet.Common namespace.
            /// </summary>
            public const EventKeywords Common = (EventKeywords)1;

            /// <summary>
            /// The event keyword for tracing events related to NuGet's configuration and settings from the NuGet.Configuration namespace.
            /// </summary>
            public const EventKeywords Configuration = (EventKeywords)2;

            /// <summary>
            /// The event keyword for tracing events related to logging.
            /// </summary>
            public const EventKeywords Logging = (EventKeywords)4;

            /// <summary>
            /// The event keyword for tracing events related to performance.
            /// </summary>
            public const EventKeywords Performance = (EventKeywords)8;

            /// <summary>
            /// The event keyword for tracing events related to the NuGet-based MSBuild project SDK resolver.
            /// </summary>
            public const EventKeywords SdkResolver = (EventKeywords)16;

            /// <summary>
            /// The event keyword for tracing events related to restore.
            /// </summary>
            public const EventKeywords Restore = (EventKeywords)32;
        }

        /// <summary>
        /// Represents event tasks for Start/Stop activity correlation.
        /// </summary>
        public static class Tasks
        {
            public const EventTask SettingsFile_FileRead = (EventTask)1;
            public const EventTask SettingsLoadingContext_FileRead = (EventTask)2;
            public const EventTask MigrationRunner_Run = (EventTask)3;
            public const EventTask RestoreCommand_BuildAssetsFile = (EventTask)4;
            public const EventTask RestoreCommand_BuildRestoreGraph = (EventTask)5;
            public const EventTask RestoreCommand_CalcNoOpRestore = (EventTask)6;
            public const EventTask RestoreRunner_RestoreProject = (EventTask)7;
            public const EventTask RestoreRunner_CommitAsync = (EventTask)8;
            public const EventTask RestoreResult_WriteAssetsFile = (EventTask)9;
            public const EventTask RestoreResult_WriteCacheFile = (EventTask)10;
            public const EventTask RestoreResult_WritePackagesLockFile = (EventTask)11;
            public const EventTask RestoreResult_WriteDgSpecFile = (EventTask)12;
            public const EventTask DependencyGraphResolver_CreateRestoreTargetGraph = (EventTask)13;
            public const EventTask DependencyGraphResolver_ResolveDependencyGraphItems = (EventTask)14;
            public const EventTask SdkResolver_GlobalJsonRead = (EventTask)15;
            public const EventTask SdkResolver_Resolve = (EventTask)16;
            public const EventTask SdkResolver_GetResult = (EventTask)17;
            public const EventTask SdkResolver_LoadSettings = (EventTask)18;
            public const EventTask SdkResolver_RestorePackage = (EventTask)19;
            public const EventTask SdkResolver_LogMessage = (EventTask)20;
        }

        // SettingsFile/FileRead (IDs 1-2)

        [Event(1, Level = EventLevel.Informational, Keywords = Keywords.Configuration, Opcode = EventOpcode.Start, Task = Tasks.SettingsFile_FileRead)]
        public void SettingsFile_FileReadStart(string configFilePath, int isMachineWide, int isReadOnly)
        {
            WriteEvent(1, configFilePath ?? string.Empty, isMachineWide, isReadOnly);
        }

        [Event(2, Level = EventLevel.Informational, Keywords = Keywords.Configuration, Opcode = EventOpcode.Stop, Task = Tasks.SettingsFile_FileRead)]
        public void SettingsFile_FileReadStop(string configFilePath, int isMachineWide, int isReadOnly)
        {
            WriteEvent(2, configFilePath ?? string.Empty, isMachineWide, isReadOnly);
        }

        // SettingsLoadingContext/FileRead (ID 3)

        [Event(3, Level = EventLevel.Informational, Keywords = Keywords.Configuration, Task = Tasks.SettingsLoadingContext_FileRead)]
        public void SettingsLoadingContext_FileRead(string fullPath, int isMachineWide, int isReadOnly)
        {
            WriteEvent(3, fullPath ?? string.Empty, isMachineWide, isReadOnly);
        }

        // MigrationRunner/Run (IDs 4-5)

        [Event(4, Level = EventLevel.Informational, Keywords = Keywords.Common | Keywords.Performance, Opcode = EventOpcode.Start, Task = Tasks.MigrationRunner_Run)]
        public void MigrationRunner_RunStart()
        {
            WriteEvent(4);
        }

        [Event(5, Level = EventLevel.Informational, Keywords = Keywords.Common | Keywords.Performance, Opcode = EventOpcode.Stop, Task = Tasks.MigrationRunner_Run)]
        public void MigrationRunner_RunStop(string migrationFileFullPath, int migrationPerformed)
        {
            WriteEvent(5, migrationFileFullPath ?? string.Empty, migrationPerformed);
        }

        // RestoreCommand/BuildAssetsFile (IDs 6-7)

        [Event(6, Level = EventLevel.Informational, Keywords = Keywords.Performance | Keywords.Restore, Opcode = EventOpcode.Start, Task = Tasks.RestoreCommand_BuildAssetsFile)]
        public void RestoreCommand_BuildAssetsFileStart(string filePath)
        {
            WriteEvent(6, filePath ?? string.Empty);
        }

        [Event(7, Level = EventLevel.Informational, Keywords = Keywords.Performance | Keywords.Restore, Opcode = EventOpcode.Stop, Task = Tasks.RestoreCommand_BuildAssetsFile)]
        public void RestoreCommand_BuildAssetsFileStop(string filePath)
        {
            WriteEvent(7, filePath ?? string.Empty);
        }

        // RestoreCommand/BuildRestoreGraph (IDs 8-9)

        [Event(8, Level = EventLevel.Informational, Keywords = Keywords.Performance | Keywords.Restore, Opcode = EventOpcode.Start, Task = Tasks.RestoreCommand_BuildRestoreGraph)]
        public void RestoreCommand_BuildRestoreGraphStart(string filePath)
        {
            WriteEvent(8, filePath ?? string.Empty);
        }

        [Event(9, Level = EventLevel.Informational, Keywords = Keywords.Performance | Keywords.Restore, Opcode = EventOpcode.Stop, Task = Tasks.RestoreCommand_BuildRestoreGraph)]
        public void RestoreCommand_BuildRestoreGraphStop(string filePath)
        {
            WriteEvent(9, filePath ?? string.Empty);
        }

        // RestoreCommand/CalcNoOpRestore (IDs 10-11)

        [Event(10, Level = EventLevel.Informational, Keywords = Keywords.Performance | Keywords.Restore, Opcode = EventOpcode.Start, Task = Tasks.RestoreCommand_CalcNoOpRestore)]
        public void RestoreCommand_CalcNoOpRestoreStart(string filePath)
        {
            WriteEvent(10, filePath ?? string.Empty);
        }

        [Event(11, Level = EventLevel.Informational, Keywords = Keywords.Performance | Keywords.Restore, Opcode = EventOpcode.Stop, Task = Tasks.RestoreCommand_CalcNoOpRestore)]
        public void RestoreCommand_CalcNoOpRestoreStop(string filePath)
        {
            WriteEvent(11, filePath ?? string.Empty);
        }

        // RestoreRunner/RestoreProject (IDs 12-13)

        [Event(12, Level = EventLevel.Informational, Keywords = Keywords.Performance | Keywords.Restore, Opcode = EventOpcode.Start, Task = Tasks.RestoreRunner_RestoreProject)]
        public void RestoreRunner_RestoreProjectStart(string filePath)
        {
            WriteEvent(12, filePath ?? string.Empty);
        }

        [Event(13, Level = EventLevel.Informational, Keywords = Keywords.Performance | Keywords.Restore, Opcode = EventOpcode.Stop, Task = Tasks.RestoreRunner_RestoreProject)]
        public void RestoreRunner_RestoreProjectStop(string filePath)
        {
            WriteEvent(13, filePath ?? string.Empty);
        }

        // RestoreRunner/CommitAsync (IDs 14-15)

        [Event(14, Level = EventLevel.Informational, Keywords = Keywords.Performance | Keywords.Restore, Opcode = EventOpcode.Start, Task = Tasks.RestoreRunner_CommitAsync)]
        public void RestoreRunner_CommitAsyncStart(string filePath)
        {
            WriteEvent(14, filePath ?? string.Empty);
        }

        [Event(15, Level = EventLevel.Informational, Keywords = Keywords.Performance | Keywords.Restore, Opcode = EventOpcode.Stop, Task = Tasks.RestoreRunner_CommitAsync)]
        public void RestoreRunner_CommitAsyncStop(string filePath)
        {
            WriteEvent(15, filePath ?? string.Empty);
        }

        // RestoreResult/WriteAssetsFile (IDs 16-17)

        [Event(16, Level = EventLevel.Informational, Keywords = Keywords.Performance | Keywords.Restore, Opcode = EventOpcode.Start, Task = Tasks.RestoreResult_WriteAssetsFile)]
        public void RestoreResult_WriteAssetsFileStart(string filePath)
        {
            WriteEvent(16, filePath ?? string.Empty);
        }

        [Event(17, Level = EventLevel.Informational, Keywords = Keywords.Performance | Keywords.Restore, Opcode = EventOpcode.Stop, Task = Tasks.RestoreResult_WriteAssetsFile)]
        public void RestoreResult_WriteAssetsFileStop(string filePath)
        {
            WriteEvent(17, filePath ?? string.Empty);
        }

        // RestoreResult/WriteCacheFile (IDs 18-19)

        [Event(18, Level = EventLevel.Informational, Keywords = Keywords.Performance | Keywords.Restore, Opcode = EventOpcode.Start, Task = Tasks.RestoreResult_WriteCacheFile)]
        public void RestoreResult_WriteCacheFileStart(string filePath)
        {
            WriteEvent(18, filePath ?? string.Empty);
        }

        [Event(19, Level = EventLevel.Informational, Keywords = Keywords.Performance | Keywords.Restore, Opcode = EventOpcode.Stop, Task = Tasks.RestoreResult_WriteCacheFile)]
        public void RestoreResult_WriteCacheFileStop(string filePath)
        {
            WriteEvent(19, filePath ?? string.Empty);
        }

        // RestoreResult/WritePackagesLockFile (IDs 20-21)

        [Event(20, Level = EventLevel.Informational, Keywords = Keywords.Performance | Keywords.Restore, Opcode = EventOpcode.Start, Task = Tasks.RestoreResult_WritePackagesLockFile)]
        public void RestoreResult_WritePackagesLockFileStart(string filePath)
        {
            WriteEvent(20, filePath ?? string.Empty);
        }

        [Event(21, Level = EventLevel.Informational, Keywords = Keywords.Performance | Keywords.Restore, Opcode = EventOpcode.Stop, Task = Tasks.RestoreResult_WritePackagesLockFile)]
        public void RestoreResult_WritePackagesLockFileStop(string filePath)
        {
            WriteEvent(21, filePath ?? string.Empty);
        }

        // RestoreResult/WriteDgSpecFile (IDs 22-23)

        [Event(22, Level = EventLevel.Informational, Keywords = Keywords.Performance | Keywords.Restore, Opcode = EventOpcode.Start, Task = Tasks.RestoreResult_WriteDgSpecFile)]
        public void RestoreResult_WriteDgSpecFileStart(string filePath)
        {
            WriteEvent(22, filePath ?? string.Empty);
        }

        [Event(23, Level = EventLevel.Informational, Keywords = Keywords.Performance | Keywords.Restore, Opcode = EventOpcode.Stop, Task = Tasks.RestoreResult_WriteDgSpecFile)]
        public void RestoreResult_WriteDgSpecFileStop(string filePath)
        {
            WriteEvent(23, filePath ?? string.Empty);
        }

        // DependencyGraphResolver/CreateRestoreTargetGraph (IDs 24-25)

        [Event(24, Level = EventLevel.Informational, Keywords = Keywords.Performance | Keywords.Restore, Opcode = EventOpcode.Start, Task = Tasks.DependencyGraphResolver_CreateRestoreTargetGraph)]
        public void DependencyGraphResolver_CreateRestoreTargetGraphStart(string filePath, string frameworkRuntimeDefinition)
        {
            WriteEvent(24, filePath ?? string.Empty, frameworkRuntimeDefinition ?? string.Empty);
        }

        [Event(25, Level = EventLevel.Informational, Keywords = Keywords.Performance | Keywords.Restore, Opcode = EventOpcode.Stop, Task = Tasks.DependencyGraphResolver_CreateRestoreTargetGraph)]
        public void DependencyGraphResolver_CreateRestoreTargetGraphStop(string filePath, string frameworkRuntimeDefinition, int success, int resolvedPackageCount, int unresolvedPackageCount)
        {
            WriteEvent(25, filePath ?? string.Empty, frameworkRuntimeDefinition ?? string.Empty, success, resolvedPackageCount, unresolvedPackageCount);
        }

        // DependencyGraphResolver/ResolveDependencyGraphItems (IDs 26-27)

        [Event(26, Level = EventLevel.Informational, Keywords = Keywords.Performance | Keywords.Restore, Opcode = EventOpcode.Start, Task = Tasks.DependencyGraphResolver_ResolveDependencyGraphItems)]
        public void DependencyGraphResolver_ResolveDependencyGraphItemsStart(string filePath, string frameworkRuntimeDefinition)
        {
            WriteEvent(26, filePath ?? string.Empty, frameworkRuntimeDefinition ?? string.Empty);
        }

        [Event(27, Level = EventLevel.Informational, Keywords = Keywords.Performance | Keywords.Restore, Opcode = EventOpcode.Stop, Task = Tasks.DependencyGraphResolver_ResolveDependencyGraphItems)]
        public void DependencyGraphResolver_ResolveDependencyGraphItemsStop(string filePath, string frameworkRuntimeDefinition, int resolvedPackagesCount, int restartCount, int totalQueuedItemCount)
        {
            WriteEvent(27, filePath ?? string.Empty, frameworkRuntimeDefinition ?? string.Empty, resolvedPackagesCount, restartCount, totalQueuedItemCount);
        }

        // SdkResolver/GlobalJsonRead (IDs 28-29)

        [Event(28, Level = EventLevel.Informational, Keywords = Keywords.SdkResolver | Keywords.Performance, Opcode = EventOpcode.Start, Task = Tasks.SdkResolver_GlobalJsonRead)]
        public void SdkResolver_GlobalJsonReadStart(string path, string projectFullPath, string solutionFullPath)
        {
            WriteEvent(28, path ?? string.Empty, projectFullPath ?? string.Empty, solutionFullPath ?? string.Empty);
        }

        [Event(29, Level = EventLevel.Informational, Keywords = Keywords.SdkResolver | Keywords.Performance, Opcode = EventOpcode.Stop, Task = Tasks.SdkResolver_GlobalJsonRead)]
        public void SdkResolver_GlobalJsonReadStop(string path, string projectFullPath, string solutionFullPath)
        {
            WriteEvent(29, path ?? string.Empty, projectFullPath ?? string.Empty, solutionFullPath ?? string.Empty);
        }

        // SdkResolver/Resolve (IDs 30-31)

        [Event(30, Level = EventLevel.Informational, Keywords = Keywords.SdkResolver | Keywords.Performance, Opcode = EventOpcode.Start, Task = Tasks.SdkResolver_Resolve)]
        public void SdkResolver_ResolveStart(string name, string version)
        {
            WriteEvent(30, name ?? string.Empty, version ?? string.Empty);
        }

        [Event(31, Level = EventLevel.Informational, Keywords = Keywords.SdkResolver | Keywords.Performance, Opcode = EventOpcode.Stop, Task = Tasks.SdkResolver_Resolve)]
        public void SdkResolver_ResolveStop(string name, string version)
        {
            WriteEvent(31, name ?? string.Empty, version ?? string.Empty);
        }

        // SdkResolver/GetResult (IDs 32-33)

        [Event(32, Level = EventLevel.Informational, Keywords = Keywords.SdkResolver | Keywords.Performance, Opcode = EventOpcode.Start, Task = Tasks.SdkResolver_GetResult)]
        public void SdkResolver_GetResultStart(string id, string version)
        {
            WriteEvent(32, id ?? string.Empty, version ?? string.Empty);
        }

        [Event(33, Level = EventLevel.Informational, Keywords = Keywords.SdkResolver | Keywords.Performance, Opcode = EventOpcode.Stop, Task = Tasks.SdkResolver_GetResult)]
        public void SdkResolver_GetResultStop(string id, string version, string installPath, int success)
        {
            WriteEvent(33, id ?? string.Empty, version ?? string.Empty, installPath ?? string.Empty, success);
        }

        // SdkResolver/LoadSettings (IDs 34-35)

        [Event(34, Level = EventLevel.Informational, Keywords = Keywords.SdkResolver | Keywords.Performance, Opcode = EventOpcode.Start, Task = Tasks.SdkResolver_LoadSettings)]
        public void SdkResolver_LoadSettingsStart()
        {
            WriteEvent(34);
        }

        [Event(35, Level = EventLevel.Informational, Keywords = Keywords.SdkResolver | Keywords.Performance, Opcode = EventOpcode.Stop, Task = Tasks.SdkResolver_LoadSettings)]
        public void SdkResolver_LoadSettingsStop()
        {
            WriteEvent(35);
        }

        // SdkResolver/RestorePackage (IDs 36-37)

        [Event(36, Level = EventLevel.Informational, Keywords = Keywords.SdkResolver | Keywords.Performance, Opcode = EventOpcode.Start, Task = Tasks.SdkResolver_RestorePackage)]
        public void SdkResolver_RestorePackageStart(string id, string version)
        {
            WriteEvent(36, id ?? string.Empty, version ?? string.Empty);
        }

        [Event(37, Level = EventLevel.Informational, Keywords = Keywords.SdkResolver | Keywords.Performance, Opcode = EventOpcode.Stop, Task = Tasks.SdkResolver_RestorePackage)]
        public void SdkResolver_RestorePackageStop(string id, string version)
        {
            WriteEvent(37, id ?? string.Empty, version ?? string.Empty);
        }

        // SdkResolver/LogMessage (ID 38)

        [Event(38, Level = EventLevel.Verbose, Keywords = Keywords.Logging, Task = Tasks.SdkResolver_LogMessage)]
        public void SdkResolver_LogMessage(int logLevel, string message)
        {
            WriteEvent(38, logLevel, message ?? string.Empty);
        }
    }
}
