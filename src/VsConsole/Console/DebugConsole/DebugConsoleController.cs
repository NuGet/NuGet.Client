// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using NuGet;

namespace NuGetConsole
{
    [Export(typeof(IDebugConsoleController))]
    public class DebugConsoleController : IDebugConsoleController
    {
        public DebugConsoleController()
        {
        }

        public void Log(DateTime timestamp, string message, TraceEventType level, string source)
        {
            if (OnMessage != null)
            {
                DebugConsoleMessageEventArgs args = new DebugConsoleMessageEventArgs(timestamp, message, level, source);

                OnMessage(this, args);
            }
        }

        public event EventHandler<DebugConsoleMessageEventArgs> OnMessage;
    }
}
