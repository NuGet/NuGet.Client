// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;

namespace NuGet.Protocol.Plugins
{
    internal static class PluginUtilities
    {
        internal static bool IsDebuggingPlugin()
        {
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NUGET_PLUGIN_DEBUG"));
        }

        internal static void WaitForAttachIfPluginDebuggingIsEnabled()
        {
            if (IsDebuggingPlugin())
            {
                while (!Debugger.IsAttached)
                {
                    Thread.Sleep(1000);
                }
            }
        }
    }
}