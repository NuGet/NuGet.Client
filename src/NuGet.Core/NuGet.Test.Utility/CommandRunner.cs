using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

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

            var output = new LockedStringBuilder();
            var errors = new LockedStringBuilder();

            Process p = null;

            try
            {
                p = new Process();

                p.OutputDataReceived += (o, e) =>
                {
                    if (e.Data != null)
                    {
                        output.AppendLine(e.Data);
                    }
                };

                p.ErrorDataReceived += (o, e) =>
                {
                    if (e.Data != null)
                    {
                        errors.AppendLine(e.Data);
                    }
                };

                p.StartInfo = psi;
                p.Start();

                p.BeginErrorReadLine();
                p.BeginOutputReadLine();

                if (inputAction != null)
                {
                    inputAction(p.StandardInput);
                }

                if (waitForExit)
                {
                    bool processExited = p.WaitForExit(timeOutInMilliseconds);
                    if (!processExited)
                    {
                        p.Kill();

                        var processName = Path.GetFileName(process);

                        throw new TimeoutException($"{processName} timed out: " + psi.Arguments);
                    }

                    if (processExited)
                    {
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

            return new CommandRunnerResult(p, exitCode, output, errors);
        }
    }
}