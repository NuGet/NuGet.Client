// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat
{
    internal interface ILoggerWithColor : ILogger
    {
        void LogMinimalWithColor(string data, ConsoleColor color);
    }
}
