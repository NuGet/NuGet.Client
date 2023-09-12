// Copyright (c) .NET Foundation. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace NuGet.Common
{
    /// <summary>
    /// Represents a class used for logging trace events from NuGet.
    /// </summary>
    [EventSource(Name = "Microsoft-NuGet")]
    public sealed class NuGetEventSource : EventSource
    {
        private NuGetEventSource()
        {
        }

        /// <summary>
        /// Gets a <see cref="NuGetEventSource" /> which can be used to trace events from NuGet.
        /// </summary>
        public static NuGetEventSource Instance { get; } = new NuGetEventSource();

        /// <summary>
        /// Writes an event indicating that a settings file read has started.
        /// </summary>
        /// <param name="path">The full path to the settings file that is being read.</param>
        /// <param name="isMachineWide"><see langword="true" /> if the settings file is machine-wide, otherwise <see langword="false" />.</param>
        /// <param name="isReadOnly"><see langword="true" /> if the settings file was loaded as read-only, otherwise <see langword="false" />.</param>
        [Event(EventId.Configuration.SettingsFileReadStart, Keywords = Keywords.Configuration | Keywords.Performance)]
        public void ConfigurationSettingsFileReadStart(string path, bool isMachineWide, bool isReadOnly)
        {
            WriteEvent(EventId.Configuration.SettingsFileReadStart, path, isMachineWide, isReadOnly);
        }

        /// <summary>
        /// Writes an event indicating that a settings file read has stopped.
        /// </summary>
        /// <param name="path">The full path to the settings file that was read.</param>
        /// <param name="isMachineWide"><see langword="true" /> if the settings file is machine-wide, otherwise <see langword="false" />.</param>
        /// <param name="isReadOnly"><see langword="true" /> if the settings file was loaded as read-only, otherwise <see langword="false" />.</param>
        [Event(EventId.Configuration.SettingsFileReadStop, Keywords = Keywords.Configuration | Keywords.Performance)]
        public void ConfigurationSettingsFileReadStop(string path, bool isMachineWide, bool isReadOnly)
        {
            WriteEvent(EventId.Configuration.SettingsFileReadStop, path, isMachineWide, isReadOnly);
        }

        /// <summary>
        /// Writes an event indicating that the SettingsLoadingContext read a file.
        /// </summary>
        /// <param name="path">The full path to the settings file that is being read.</param>
        /// <param name="isMachineWide"><see langword="true" /> if the settings file is machine-wide, otherwise <see langword="false" />.</param>
        /// <param name="isReadOnly"><see langword="true" /> if the settings file was loaded as read-only, otherwise <see langword="false" />.</param>
        [Event(EventId.Configuration.SettingsLoadingContextFileRead, Keywords = Keywords.Configuration)]
        public void ConfigurationSettingsLoadingContextFileRead(string path, bool isMachineWide, bool isReadOnly)
        {
            WriteEvent(EventId.Configuration.SettingsLoadingContextFileRead, path, isMachineWide, isReadOnly);
        }

        /// <summary>
        /// Writes an event indicating that an message was logged.
        /// </summary>
        /// <param name="level">The <see cref="LogLevel" /> of the message.</param>
        /// <param name="message">The message.</param>
        [Event(EventId.Common.LogMessage, Keywords = Keywords.Logging)]
        public void LogMessage(LogLevel level, string message)
        {
            WriteEvent(EventId.Common.LogMessage, level, message);
        }

        /// <summary>
        /// Writes an event indicating that the <see cref="Migrations.MigrationRunner" /> has started.
        /// </summary>
        [Event(EventId.Common.MigrationRunnerStart, Keywords = Keywords.Common)]
        public void MigrationRunnerStart()
        {
            WriteEvent(EventId.Common.MigrationRunnerStart);
        }

        /// <summary>
        /// Writes an event indicating that the <see cref="Migrations.MigrationRunner" /> has stopped.
        /// </summary>
        /// <param name="migrationFileFullPath">The full path to the expected migration file.</param>
        /// <param name="migrationPerformed"><see langword="true" /> if a migration was actually performed, otherwise <see langword="false" />.</param>
        [Event(EventId.Common.MigrationRunnerStop, Keywords = Keywords.Common)]
        public void MigrationRunnerStop(string migrationFileFullPath, bool migrationPerformed)
        {
            WriteEvent(EventId.Common.MigrationRunnerStop, migrationFileFullPath, migrationPerformed);
        }

        /// <summary>
        /// Writes an event indicating that the NuGet-based MSBuild project SDK resolver's GetResult operation has started.
        /// </summary>
        /// <param name="id">The ID of a package.</param>
        /// <param name="version">The version of a package.</param>
        [Event(EventId.SdkResolver.GetResultStart, Keywords = Keywords.Performance | Keywords.SdkResolver)]
        public void SdkResolverGetResultStart(string id, string version)
        {
            WriteEvent(EventId.SdkResolver.GetResultStart, id, version);
        }

        /// <summary>
        /// Writes an event indicating that the NuGet-based MSBuild project SDK resolver's GetResult operation has stopped.
        /// </summary>
        /// <param name="id">The ID of a package.</param>
        /// <param name="version">The version of a package.</param>
        /// <param name="installPath">The resolved path of a package.</param>
        /// <param name="success"><see langword="true" /> if the operation succeeded, otherwise <see langword="false" />.</param>
        [Event(EventId.SdkResolver.GetResultStop, Keywords = Keywords.Performance | Keywords.SdkResolver)]
        public void SdkResolverGetResultStop(string id, string version, string installPath, bool success)
        {
            WriteEvent(EventId.SdkResolver.GetResultStop, id, version, installPath, success);
        }

        /// <summary>
        /// Writes an event indicating that the NuGet-based MSBuild project SDK resolver's GlobalJsonRead operation has started.
        /// </summary>
        /// <param name="path">The path to global.json.</param>
        /// <param name="projectPath">The path to the project.</param>
        /// <param name="solutionPath">The path to the solution.</param>
        [Event(EventId.SdkResolver.GlobalJsonReadStart, Keywords = Keywords.Performance | Keywords.SdkResolver)]
        public void SdkResolverGlobalJsonReadStart(string path, string projectPath, string solutionPath)
        {
            WriteEvent(EventId.SdkResolver.GlobalJsonReadStart, path, projectPath, solutionPath);
        }

        /// <summary>
        /// Writes an event indicating that the NuGet-based MSBuild project SDK resolver's GlobalJsonRead operation has stopped.
        /// </summary>
        /// <param name="path">The path to global.json.</param>
        /// <param name="projectPath">The path to the project.</param>
        /// <param name="solutionPath">The path to the solution.</param>
        [Event(EventId.SdkResolver.GlobalJsonReadStop, Keywords = Keywords.Performance | Keywords.SdkResolver)]
        public void SdkResolverGlobalJsonReadStop(string path, string projectPath, string solutionPath)
        {
            WriteEvent(EventId.SdkResolver.GlobalJsonReadStop, path, projectPath, solutionPath);
        }

        /// <summary>
        /// Writes an event indicating that the NuGet-based MSBuild project SDK resolver's settings load operation has started.
        /// </summary>
        [Event(EventId.SdkResolver.LoadSettingsStart, Keywords = Keywords.Performance | Keywords.SdkResolver)]
        public void SdkResolverLoadSettingsStart()
        {
            WriteEvent(EventId.SdkResolver.LoadSettingsStart);
        }

        /// <summary>
        /// Writes an event indicating that the NuGet-based MSBuild project SDK resolver's settings load operation has stopped.
        /// </summary>
        [Event(EventId.SdkResolver.LoadSettingsStop, Keywords = Keywords.Performance | Keywords.SdkResolver)]
        public void SdkResolverLoadSettingsStop()
        {
            WriteEvent(EventId.SdkResolver.LoadSettingsStop);
        }

        /// <summary>
        /// Writes an event indicating that the NuGet-based MSBuild project SDK resolver's Resolve operation has started.
        /// </summary>
        /// <param name="id">The ID of a package.</param>
        /// <param name="version">The version of a package.</param>
        [Event(EventId.SdkResolver.ResolveStart, Keywords = Keywords.Performance | Keywords.SdkResolver)]
        public void SdkResolverResolveStart(string id, string version)
        {
            WriteEvent(EventId.SdkResolver.ResolveStart, id, version);
        }

        /// <summary>
        /// Writes an event indicating that the NuGet-based MSBuild project SDK resolver's Resolve operation has stopped.
        /// </summary>
        /// <param name="id">The ID of a package.</param>
        /// <param name="version">The version of a package.</param>
        [Event(EventId.SdkResolver.ResolveStop, Keywords = Keywords.Performance | Keywords.SdkResolver)]
        public void SdkResolverResolveStop(string id, string version)
        {
            WriteEvent(EventId.SdkResolver.ResolveStop, id, version);
        }

        /// <summary>
        /// Writes an event indicating that the NuGet-based MSBuild project SDK resolver's RestorePackage operation has started.
        /// </summary>
        /// <param name="id">The ID of the package.</param>
        /// <param name="originalVersion">The version of the package.</param>
        [Event(EventId.SdkResolver.RestorePackageStart, Keywords = Keywords.Performance | Keywords.SdkResolver)]
        public void SdkResolverRestorePackageStart(string id, string originalVersion)
        {
            WriteEvent(EventId.SdkResolver.RestorePackageStart, id, originalVersion);
        }

        /// <summary>
        /// Writes an event indicating that the NuGet-based MSBuild project SDK resolver's RestorePackage operation has stopped.
        /// </summary>
        /// <param name="id">The ID of the package.</param>
        /// <param name="originalVersion">The version of the package.</param>
        [Event(EventId.SdkResolver.RestorePackageStop, Keywords = Keywords.Performance | Keywords.SdkResolver)]
        public void SdkResolverRestorePackageStop(string id, string originalVersion)
        {
            WriteEvent(EventId.SdkResolver.RestorePackageStop, id, originalVersion);
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
            /// The event keyword for tracing events related to the NuGet-based MSBuild project SDK resolver.
            /// </summary>
            public const EventKeywords SdkResolver = (EventKeywords)16;
        }

        /// <summary>
        /// Represents a class for declaring event identifiers used for logging trace events from NuGet.
        /// </summary>
        private static class EventId
        {
            /// <summary>
            /// Represents events from the NuGet.Common namespace with event identifiers between 1 and 99.
            /// </summary>
            public static class Common
            {
                public const int LogMessage = 2;
                public const int LogMessageError = 1;
                public const int LogMessageMinimal = 3;
                public const int LogMessageVerbose = 4;
                public const int LogMessageWarning = 5;
                public const int MigrationRunnerStart = 6;
                public const int MigrationRunnerStop = 7;
            }

            /// <summary>
            /// Represents events from the NuGet.Configuration namespace with event identifiers between 100 and 199.
            /// </summary>
            public static class Configuration
            {
                public const int SettingsFileReadStart = 100;
                public const int SettingsFileReadStop = 101;
                public const int SettingsLoadingContextFileRead = 102;
            }

            /// <summary>
            /// Represents events from the Microsoft.Build.NuGetSdkResolver namespace with event identifiers between 200 and 299.
            /// </summary>
            public static class SdkResolver
            {
                public const int GetResultStart = 200;
                public const int GetResultStop = 201;
                public const int GlobalJsonReadStart = 202;
                public const int GlobalJsonReadStop = 203;
                public const int LoadSettingsStart = 204;
                public const int LoadSettingsStop = 205;
                public const int ResolveStart = 206;
                public const int ResolveStop = 207;
                public const int RestorePackageStart = 208;
                public const int RestorePackageStop = 209;
            }
        }
    }
}
