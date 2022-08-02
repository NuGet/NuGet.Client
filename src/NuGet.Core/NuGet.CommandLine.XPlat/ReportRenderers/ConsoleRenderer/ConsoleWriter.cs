// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.CommandLine.XPlat.ReportRenderers.ConsoleRenderer
{
    internal class ConsoleWriter : IReportRenderer
    {
        private int _problemCount;
        private ConsoleWriter()
        { }

        public void WriteErrorLine(string errorText, string _)
        {
            _problemCount++;
            Console.Error.WriteLine(errorText);
        }

        public void Write(string value)
        {
            Console.Write(value);
        }

        public void WriteLine()
        {
            Console.WriteLine();
        }

        public void WriteLine(string value)
        {
            Console.WriteLine(value);
        }

        public void SetForegroundColor(ConsoleColor consoleColor)
        {
            Console.ForegroundColor = consoleColor;
        }

        public void ResetColor()
        {
            Console.ResetColor();
        }

        public void LogParameters(string parameters)
        {
            // do nothing, cli no need to log parameters.
        }

        public void FinishRendering()
        {
            // do nothing
        }

        public static ConsoleWriter Instance { get; } = new ConsoleWriter();

        public int ExitCode() => _problemCount > 0 ? 1 : 0;
    }
}
