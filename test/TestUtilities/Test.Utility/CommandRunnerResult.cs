// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;

namespace NuGet.Test.Utility
{
    public class CommandRunnerResult
    {
        public Process Process { get; }

        /// <summary>
        /// Item 1. Multi-purpose
        /// </summary>
        /// <remarks>
        /// In occasions, it refers to Exit Status Code of the command execution result
        /// </remarks>
        public int Item1 { get; }

        /// <summary>
        /// Item 2. Multi-purpose
        /// </summary>
        /// <remarks>
        /// In occasions, it refers to the Standard Output of the command execution
        /// </remarks>
        public string Item2 { get; }

        /// <summary>
        /// Item 3. Multi-purpose
        /// </summary>
        /// <remarks>
        /// In occasions, it refers to the Standard Error of the command execution
        /// </remarks>
        public string Item3 { get; }

        public int ExitCode => Item1;

        public bool Success => Item1 == 0;

        /// <summary>
        /// All output messages including errors
        /// </summary>
        public string AllOutput => Item2 + Environment.NewLine + Item3;

        public string Output => Item2;

        public string Errors => Item3;

        internal CommandRunnerResult(Process process, int exitCode, string output, string error)
        {
            Process = process;
            Item1 = exitCode;
            Item2 = output;
            Item3 = error;
        }
    }
}
