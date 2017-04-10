// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;

namespace NuGet.Test.Utility
{
    public class CommandRunnerResult
    {
        public Process Process { get; }
        public int Item1 { get; }
        public string Item2 { get; }
        public string Item3 { get; }

        internal CommandRunnerResult(Process process, int exitCode, string output, string error)
        {
            Process = process;
            Item1 = exitCode;
            Item2 = output;
            Item3 = error;
        }
    }
}