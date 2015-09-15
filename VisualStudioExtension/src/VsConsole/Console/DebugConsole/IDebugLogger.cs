// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetConsole.Implementation
{
    public interface IDebugLogger
    {
        void Log(string message, ConsoleColor color);

        void SetConsole(DebugConsoleToolWindow console);
    }
}
