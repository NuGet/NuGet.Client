// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Diagnostics.Tracing;

namespace NuGet.VisualStudio.Etw
{
    public static class NuGetExtensibilityEtw
    {
        public static EventSource EventSource { get; } = new EventSource("NuGet-VS-Extensibility");

        public static EventSourceOptions StartEventOptions { get; } =
            new EventSourceOptions()
            {
                Opcode = EventOpcode.Start,
                Level = EventLevel.Informational,
                ActivityOptions = EventActivityOptions.Detachable
            };

        public static EventSourceOptions StopEventOptions { get; } =
            new EventSourceOptions()
            {
                Opcode = EventOpcode.Stop,
                Level = EventLevel.Informational,
                ActivityOptions = EventActivityOptions.Detachable
            };

        public static EventSourceOptions AddEventOptions { get; } =
            new EventSourceOptions()
            {
                Opcode = CustomOpcodes.Add,
                Level = EventLevel.Informational,
                ActivityOptions = EventActivityOptions.Detachable
            };

        public static EventSourceOptions RemoveEventOptions { get; } =
            new EventSourceOptions()
            {
                Opcode = CustomOpcodes.Remove,
                Level = EventLevel.Informational,
                ActivityOptions = EventActivityOptions.Detachable
            };

        public static EventSourceOptions InfoEventOptions { get; } =
            new EventSourceOptions()
            {
                Opcode = EventOpcode.Info,
                Level = EventLevel.Informational,
                ActivityOptions = EventActivityOptions.None
            };

        public static class CustomOpcodes
        {
            public static readonly EventOpcode Add = (EventOpcode)11;
            public static readonly EventOpcode Remove = (EventOpcode)12;
        }
    }
}
