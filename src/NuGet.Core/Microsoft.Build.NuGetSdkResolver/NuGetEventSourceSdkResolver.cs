// Copyright (c) .NET Foundation. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;

namespace Microsoft.Build.NuGetSdkResolver
{
    [EventSource(Name = "Microsoft-NuGet-SdkResolver")]
    internal sealed class NuGetEventSourceSdkResolver : EventSource
    {
        public static readonly NuGetEventSourceSdkResolver Instance = new();

        private NuGetEventSourceSdkResolver() { }

        public static class Keywords
        {
            public const EventKeywords Logging = (EventKeywords)4;
            public const EventKeywords Performance = (EventKeywords)8;
            public const EventKeywords SdkResolver = (EventKeywords)16;
        }

        public static class Tasks
        {
            public const EventTask GlobalJsonRead = (EventTask)1;
            public const EventTask Resolve = (EventTask)2;
            public const EventTask GetResult = (EventTask)3;
            public const EventTask LoadSettings = (EventTask)4;
            public const EventTask RestorePackage = (EventTask)5;
            public const EventTask LogMessage = (EventTask)6;
        }

        // GlobalJsonRead (IDs 1-2)

        [Event(1, Level = EventLevel.Informational, Keywords = Keywords.SdkResolver | Keywords.Performance, Opcode = EventOpcode.Start, Task = Tasks.GlobalJsonRead)]
        public void GlobalJsonReadStart(string path, string projectFullPath, string solutionFullPath)
        {
            WriteEvent(1, path ?? string.Empty, projectFullPath ?? string.Empty, solutionFullPath ?? string.Empty);
        }

        [Event(2, Level = EventLevel.Informational, Keywords = Keywords.SdkResolver | Keywords.Performance, Opcode = EventOpcode.Stop, Task = Tasks.GlobalJsonRead)]
        public void GlobalJsonReadStop(string path, string projectFullPath, string solutionFullPath)
        {
            WriteEvent(2, path ?? string.Empty, projectFullPath ?? string.Empty, solutionFullPath ?? string.Empty);
        }

        // Resolve (IDs 3-4)

        [Event(3, Level = EventLevel.Informational, Keywords = Keywords.SdkResolver | Keywords.Performance, Opcode = EventOpcode.Start, Task = Tasks.Resolve)]
        public void ResolveStart(string name, string version)
        {
            WriteEvent(3, name ?? string.Empty, version ?? string.Empty);
        }

        [Event(4, Level = EventLevel.Informational, Keywords = Keywords.SdkResolver | Keywords.Performance, Opcode = EventOpcode.Stop, Task = Tasks.Resolve)]
        public void ResolveStop(string name, string version)
        {
            WriteEvent(4, name ?? string.Empty, version ?? string.Empty);
        }

        // GetResult (IDs 5-6)

        [Event(5, Level = EventLevel.Informational, Keywords = Keywords.SdkResolver | Keywords.Performance, Opcode = EventOpcode.Start, Task = Tasks.GetResult)]
        public void GetResultStart(string id, string version)
        {
            WriteEvent(5, id ?? string.Empty, version ?? string.Empty);
        }

        [Event(6, Level = EventLevel.Informational, Keywords = Keywords.SdkResolver | Keywords.Performance, Opcode = EventOpcode.Stop, Task = Tasks.GetResult)]
        public void GetResultStop(string id, string version, string installPath, int success)
        {
            WriteEvent(6, id ?? string.Empty, version ?? string.Empty, installPath ?? string.Empty, success);
        }

        // LoadSettings (IDs 7-8)

        [Event(7, Level = EventLevel.Informational, Keywords = Keywords.SdkResolver | Keywords.Performance, Opcode = EventOpcode.Start, Task = Tasks.LoadSettings)]
        public void LoadSettingsStart()
        {
            WriteEvent(7);
        }

        [Event(8, Level = EventLevel.Informational, Keywords = Keywords.SdkResolver | Keywords.Performance, Opcode = EventOpcode.Stop, Task = Tasks.LoadSettings)]
        public void LoadSettingsStop()
        {
            WriteEvent(8);
        }

        // RestorePackage (IDs 9-10)

        [Event(9, Level = EventLevel.Informational, Keywords = Keywords.SdkResolver | Keywords.Performance, Opcode = EventOpcode.Start, Task = Tasks.RestorePackage)]
        public void RestorePackageStart(string id, string version)
        {
            WriteEvent(9, id ?? string.Empty, version ?? string.Empty);
        }

        [Event(10, Level = EventLevel.Informational, Keywords = Keywords.SdkResolver | Keywords.Performance, Opcode = EventOpcode.Stop, Task = Tasks.RestorePackage)]
        public void RestorePackageStop(string id, string version)
        {
            WriteEvent(10, id ?? string.Empty, version ?? string.Empty);
        }

        // LogMessage (ID 11)

        [Event(11, Level = EventLevel.Verbose, Keywords = Keywords.Logging, Task = Tasks.LogMessage)]
        public void LogMessage(int logLevel, string message)
        {
            WriteEvent(11, logLevel, message ?? string.Empty);
        }
    }
}
