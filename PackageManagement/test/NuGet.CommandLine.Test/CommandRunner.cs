using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.CommandLine.Test
{
    public class CommandRunner
    {
        // Item1 of the returned tuple is the exit code. Item2 is the standard output, and Item3 
        // is the error output.
        public static Tuple<int, string, string> Run(
            string process, 
            string workingDirectory, 
            string arguments, 
            bool waitForExit, 
            int timeOutInMilliseconds = 60000,
           Action<StreamWriter> inputAction = null)
        {
            string result = String.Empty;
            string error = String.Empty;

            ProcessStartInfo psi = new ProcessStartInfo(Path.GetFullPath(process), arguments)
            {
                WorkingDirectory = Path.GetFullPath(workingDirectory),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = inputAction != null
            };

            StreamReader standardOutput;
            StreamReader errorOutput;
            int exitCode = 1;

            using (Process p = new Process())
            {
                p.StartInfo = psi;
                p.Start();
                standardOutput = p.StandardOutput;
                errorOutput = p.StandardError;

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
                    }
                }

                result = standardOutput.ReadToEnd();
                error = errorOutput.ReadToEnd();

                if (p.HasExited)
                {
                    exitCode = p.ExitCode;
                }
            }

            return Tuple.Create(exitCode, result, error);
        }
    }
}