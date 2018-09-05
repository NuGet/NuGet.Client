// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Test.Utility
{
    public class CommandRunner
    {
        // Item1 of the returned tuple is the exit code. Item2 is the standard output, and Item3
        // is the error output.
        public static CommandRunnerResult Run(
            string process,
            string workingDirectory,
            string arguments,
            bool waitForExit,
            int timeOutInMilliseconds = 60000,
            Action<StreamWriter> inputAction = null,
            bool shareProcessObject = false,
            IDictionary<string, string> environmentVariables = null)
        {
            var psi = new ProcessStartInfo(Path.GetFullPath(process), arguments)
            {
                WorkingDirectory = Path.GetFullPath(workingDirectory),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = inputAction != null
            };

#if !IS_CORECLR
            psi.EnvironmentVariables["NuGetTestModeEnabled"] = "True";
#else
            psi.Environment["NuGetTestModeEnabled"] = "True";
#endif

            if (environmentVariables != null)
            {
                foreach (var pair in environmentVariables)
                {
#if !IS_CORECLR
                    psi.EnvironmentVariables[pair.Key] = pair.Value;
#else
                    psi.Environment[pair.Key] = pair.Value;
#endif
                }
            }

            int exitCode = 1;

            var output = new StringBuilder();
            var errors = new StringBuilder();

            Process p = null;

            try
            {
                p = new Process();

                p.StartInfo = psi;
                p.Start();

                var outputTask = ConsumeStreamReaderAsync(p.StandardOutput, output);
                var errorTask = ConsumeStreamReaderAsync(p.StandardError, errors);

                inputAction?.Invoke(p.StandardInput);

                if (waitForExit)
                {
#if DEBUG
                    var processExited = true;
                    p.WaitForExit();
#else
                    var processExited = p.WaitForExit(timeOutInMilliseconds);
#endif
                    if (!processExited)
                    {
                        p.Kill();

                        var processName = Path.GetFileName(process);

                        throw new TimeoutException($"{processName} timed out: " + psi.Arguments);
                    }

                    if (processExited)
                    {
                        Task.WaitAll(outputTask, errorTask);
                        exitCode = p.ExitCode;
                    }
                }
            }
            finally
            {
                if (!shareProcessObject)
                {
                    p.Dispose();
                }
            }

            return new CommandRunnerResult(p, exitCode, output.ToString(), errors.ToString());
        }

        private static async Task ConsumeStreamReaderAsync(StreamReader reader, StringBuilder lines)
        {
            await Task.Yield();

            string line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                lines.AppendLine(line);
            }
        }
    }
}