// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;
using System.Threading.Tasks;
using NuGet.Common;

namespace Microsoft.Build.NuGetSdkResolver
{
    /// <summary>
    /// Represents an <see cref="EventSource" /> for the NuGet-based MSBuild project SDK resolver.
    /// </summary>
    [EventSource(Name = "Microsoft-NuGet-SdkResolver")]
    internal class NuGetSdkResolverEventSource : EventSource
    {
        /// <summary>
        ///  The current version of events.  If any method signature is changed, you must increment this.
        /// </summary>
        public const int EventVersion = 20;

        private NuGetSdkResolverEventSource()
        {
        }

        /// <summary>
        /// Gets an instance of the <see cref="NuGetSdkResolverEventSource" /> class.
        /// </summary>
        public static NuGetSdkResolverEventSource Instance { get; } = new NuGetSdkResolverEventSource();

        /// <summary>
        /// Gets an <see cref="ILogger" /> that can be used to log messages to this event source.
        /// </summary>
        public static ILogger Logger { get; } = new EventSourceLogger();

        /// <summary>
        /// Writes an event indicating that a GetResult operation has started.
        /// </summary>
        /// <param name="id">The ID of a package.</param>
        /// <param name="version">The version of a package.</param>
        [Event(EventId.GetResultStart, Keywords = Keywords.All, Version = EventVersion)]
        public void GetResultStart(string id, string version)
        {
            WriteEvent(EventId.GetResultStart, id, version);
        }

        /// <summary>
        /// Writes an event indicating that a GetResult operation has completed.
        /// </summary>
        /// <param name="id">The ID of a package.</param>
        /// <param name="version">The version of a package.</param>
        /// <param name="installPath">The resolved path of a package.</param>
        /// <param name="success"><c>true</c> if the operation succeeded, otherwise <c>false</c>.</param>
        [Event(EventId.GetResultStop, Keywords = Keywords.All, Version = EventVersion)]
        public void GetResultStop(string id, string version, string installPath, bool success)
        {
            WriteEvent(EventId.GetResultStop, id, version, installPath, success);
        }

        /// <summary>
        /// Writes an event indicating that a Resolve operation has started.
        /// </summary>
        /// <param name="id">The ID of a package.</param>
        /// <param name="version">The version of a package.</param>
        [Event(EventId.ResolveStart, Keywords = Keywords.All, Version = EventVersion)]
        public void ResolveStart(string id, string version)
        {
            WriteEvent(EventId.ResolveStart, id, version);
        }

        /// <summary>
        /// Writes an event indicating that a Resolve operation has completed.
        /// </summary>
        /// <param name="id">The ID of a package.</param>
        /// <param name="version">The version of a package.</param>
        [Event(EventId.ResolveStop, Keywords = Keywords.All, Version = EventVersion)]
        public void ResolveStop(string id, string version)
        {
            WriteEvent(EventId.ResolveStop, id, version);
        }

        /// <summary>
        /// Writes an event indicating that a GlobalJsonRead operation has started.
        /// </summary>
        /// <param name="path">The path to global.json.</param>
        /// <param name="projectPath">The path to the project.</param>
        /// <param name="solutionPath">The path to the solution.</param>
        [Event(EventId.GlobalJsonReadStart, Keywords = Keywords.All, Version = EventVersion)]
        internal void GlobalJsonReadStart(string path, string projectPath, string solutionPath)
        {
            WriteEvent(EventId.GlobalJsonReadStart, path, projectPath, solutionPath);
        }

        /// <summary>
        /// Writes an event indicating that a GlobalJsonRead operation has completed.
        /// </summary>
        /// <param name="path">The path to global.json.</param>
        /// <param name="projectPath">The path to the project.</param>
        /// <param name="solutionPath">The path to the solution.</param>
        [Event(EventId.GlobalJsonReadStop, Keywords = Keywords.All, Version = EventVersion)]
        internal void GlobalJsonReadStop(string path, string projectPath, string solutionPath)
        {
            WriteEvent(EventId.GlobalJsonReadStop, path, projectPath, solutionPath);
        }

        /// <summary>
        /// Writes an event indicating that a message was logged.
        /// </summary>
        /// <param name="logLevel">The <see cref="LogLevel" /> of the message.</param>
        /// <param name="message">The message.</param>
        [Event(EventId.LogMessage, Keywords = Keywords.All, Version = EventVersion)]
        internal void LogMessage(LogLevel logLevel, string message)
        {
            WriteEvent(EventId.LogMessage, logLevel, message);
        }

        /// <summary>
        /// Writes an event indicating that a RestorePackage operation has started.
        /// </summary>
        /// <param name="id">The ID of the package.</param>
        /// <param name="originalVersion">The version of the package.</param>
        [Event(EventId.RestorePackageStart, Keywords = Keywords.All, Version = EventVersion)]
        internal void RestorePackageStart(string id, string originalVersion)
        {
            WriteEvent(EventId.RestorePackageStart, id, originalVersion);
        }

        /// <summary>
        /// Writes an event indicating that a RestorePackage operation has completed.
        /// </summary>
        /// <param name="id">The ID of the package.</param>
        /// <param name="originalVersion">The version of the package.</param>
        [Event(EventId.RestorePackageStop, Keywords = Keywords.All, Version = EventVersion)]
        internal void RestorePackageStop(string id, string originalVersion)
        {
            WriteEvent(EventId.RestorePackageStop, id, originalVersion);
        }

        /// <summary>
        /// Writes an event indicating that a WaitForRestoreSemaphore operation has started.
        /// </summary>
        /// <param name="id">The ID of the package.</param>
        /// <param name="originalVersion">The version of the package.</param>
        [Event(EventId.WaitForRestoreSemaphoreStart, Keywords = Keywords.All, Version = EventVersion)]
        internal void WaitForRestoreSemaphoreStart(string id, string originalVersion)
        {
            WriteEvent(EventId.WaitForRestoreSemaphoreStart, id, originalVersion);
        }

        /// <summary>
        /// Writes an event indicating that a WaitForRestoreSemaphore operation has completed.
        /// </summary>
        /// <param name="id">The ID of the package.</param>
        /// <param name="originalVersion">The version of the package.</param>
        [Event(EventId.WaitForRestoreSemaphoreStop, Keywords = Keywords.All, Version = EventVersion)]
        internal void WaitForRestoreSemaphoreStop(string id, string originalVersion)
        {
            WriteEvent(EventId.WaitForRestoreSemaphoreStop, id, originalVersion);
        }

        private static class EventId
        {
            public const int GetResultStart = 1;
            public const int GetResultStop = 2;
            public const int GlobalJsonReadStart = 5;
            public const int GlobalJsonReadStop = 6;
            public const int LogMessage = 7;
            public const int ResolveStart = 3;
            public const int ResolveStop = 4;
            public const int RestorePackageStart = 8;
            public const int RestorePackageStop = 9;
            public const int WaitForRestoreSemaphoreStart = 10;
            public const int WaitForRestoreSemaphoreStop = 11;
        }

        public static class Keywords
        {
            /// <summary>
            /// Keyword applied to all events.
            /// </summary>
            public const EventKeywords All = (EventKeywords)0x1;
        }

        private class EventSourceLogger : LoggerBase
        {
            public override void Log(ILogMessage message)
            {
                if (message.Message != null)
                {
                    Instance.LogMessage(message.Level, message.Message);
                }
            }

            public override Task LogAsync(ILogMessage message)
            {
                Log(message);

                return Task.CompletedTask;
            }
        }
    }
}
