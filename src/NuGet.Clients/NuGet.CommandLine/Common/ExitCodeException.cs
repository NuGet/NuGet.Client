﻿using System;

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