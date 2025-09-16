// Copyright (c) .NET Foundation. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace NuGet.Common
{
    /// <summary>
    /// Represents a class used for logging trace events from NuGet.
    /// </summary>
    public sealed class NuGetEventSource : EventSource
    {
        private NuGetEventSource()
            : base("Microsoft-NuGet")

        {
        }

        /// <summary>
        /// Gets a <see cref="NuGetEventSource" /> which can be used to trace events from NuGet.
        /// </summary>
        public static NuGetEventSource Instance { get; } = new NuGetEventSource();

        [Event(2, Level = EventLevel.Informational, Message = "Event: {0}", Opcode = EventOpcode.Start, Keywords = Keywords.Common | Keywords.Performance, ActivityOptions = EventActivityOptions.Detachable)]
        public void MigrationRunnerStart()
        {
            WriteEvent(3, "MigrationRunner/Run");
        }

        [Event(3, Level = EventLevel.Informational, Message = "Event: {0}, MigrationFileFullPath: {1}, MigrationPerformed: {2}", Opcode = EventOpcode.Stop, Keywords = Keywords.Common | Keywords.Performance, ActivityOptions = EventActivityOptions.Detachable)]
        public void MigrationRunnerStop(string migrationFilePath, bool migrationPerformed)
        {
            WriteEvent(2, "MigrationRunner/Run", migrationFilePath, migrationPerformed);
        }

        [Event(4, Level = EventLevel.Informational, Message = "Event: {0}, ConfigFilePath: {1}, IsMachineWide: {2}, IsReadOnly: {3}", Opcode = EventOpcode.Start, Keywords = Keywords.Configuration | Keywords.Performance, ActivityOptions = EventActivityOptions.Detachable)]
        public void SettingsFileReadStart(string configFilePath, bool isMachineWide, bool isReadOnly)
        {
            WriteEvent(4, "SettingsFile/FileRead", configFilePath, isMachineWide, isReadOnly);
        }

        [Event(4, Level = EventLevel.Informational, Message = "Event: {0}, ConfigFilePath: {1}, IsMachineWide: {2}, IsReadOnly: {3}", Opcode = EventOpcode.Stop, Keywords = Keywords.Configuration | Keywords.Performance, ActivityOptions = EventActivityOptions.Detachable)]
        public void SettingsFileReadStop(string configFilePath, bool isMachineWide, bool isReadOnly)
        {
            WriteEvent(4, "SettingsFile/FileRead", configFilePath, isMachineWide, isReadOnly);
        }

        [Event(1, Level = EventLevel.Informational, Message = "Event: {0}, FilePath: {1}, IsMachineWide: {2}, IsReadOnly: {3}", Opcode = EventOpcode.Info, Keywords = Keywords.Configuration)]
        public void SettingsLoadingContextFileRead(string filePath, bool isMachineWide, bool isReadOnly)
        {
            WriteEvent(1, "SettingsLoadingContext/FileRead", filePath, isMachineWide, isReadOnly);
        }

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
            /// The event keyword for tracing events related to restore.
            /// </summary>
            public const EventKeywords Restore = (EventKeywords)32;

            /// <summary>
            /// The event keyword for tracing events related to the NuGet-based MSBuild project SDK resolver.
            /// </summary>
            public const EventKeywords SdkResolver = (EventKeywords)16;
        }
    }
}
