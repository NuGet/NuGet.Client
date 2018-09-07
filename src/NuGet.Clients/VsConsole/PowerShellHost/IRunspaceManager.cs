// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetConsole.Host.PowerShell.Implementation
{
    internal interface IRunspaceManager
    {
        Tuple<RunspaceDispatcher, NuGetPSHost> GetRunspace(IConsole console, string hostName);
    }
}
