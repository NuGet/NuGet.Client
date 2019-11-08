using System;
using NuGet.Commands;

namespace NuGet.CommandLine
{
    [Serializable]
    public class ExitCodeException : CommandLineException
    {
        public ExitCodeException(int exitCode)
        {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }
}
