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
        /// Refers to Exit Status Code of the command execution result
        /// </summary>
        public int ExitCode { get; }

        /// <summary>
        /// refers to the Standard Output of the command execution
        /// </summary>
        public string Output { get; }

        /// <summary>
        /// Refers to the Standard Error of the command execution
        /// </summary>
        public string Errors { get; }

        public bool Success => ExitCode == 0;

        /// <summary>
        /// All output messages including errors
        /// </summary>
        public string AllOutput => Output + Environment.NewLine + Errors;

        internal CommandRunnerResult(Process process, int exitCode, string output, string error)
        {
            Process = process;
            ExitCode = exitCode;
            Output = output;
            Errors = error;
        }
    }
}
