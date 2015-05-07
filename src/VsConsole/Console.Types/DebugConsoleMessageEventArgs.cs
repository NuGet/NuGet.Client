// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;

namespace NuGet
{
    public class DebugConsoleMessageEventArgs : EventArgs
    {
        public string Message { get; private set; }
        public TraceEventType Level { get; private set; }
        public string Source { get; private set; }
        public DateTime Timestamp { get; private set; }

        public DebugConsoleMessageEventArgs(DateTime timestamp, string message, TraceEventType level, string source)
        {
            Timestamp = timestamp;
            Message = message;
            Level = level;
            Source = source;
        }
    }
}
