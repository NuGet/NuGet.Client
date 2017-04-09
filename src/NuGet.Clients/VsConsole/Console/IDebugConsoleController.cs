// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;

namespace NuGet
{
    public interface IDebugConsoleController
    {
        /// <summary>
        /// Raised when the source needs to log a message.
        /// </summary>
        event EventHandler<DebugConsoleMessageEventArgs> OnMessage;

        void Log(DateTime timestamp, string message, TraceEventType level, string source);
    }
}
