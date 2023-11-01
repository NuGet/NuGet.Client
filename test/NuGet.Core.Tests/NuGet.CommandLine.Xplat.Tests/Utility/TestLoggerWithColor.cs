// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using NuGet.CommandLine.XPlat;
using NuGet.Test.Utility;

namespace NuGet.CommandLine.Xplat
{
    internal class TestLoggerWithColor : TestLogger, ILoggerWithColor
    {
        public ConcurrentQueue<Tuple<string, ConsoleColor>> MessagesWithColor { get; } = new ConcurrentQueue<Tuple<string, ConsoleColor>>();

        public void LogMinimalWithColor(string data, ConsoleColor color)
        {
            MessagesWithColor.Enqueue(new Tuple<string, ConsoleColor>(data, color));
        }
    }
}
