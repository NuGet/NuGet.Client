// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Diagnostics.Tracing;

namespace NuGet.VisualStudio.Implementation.Extensibility
{
    internal static class NuGetExtensibilityEtw
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
    }
}
