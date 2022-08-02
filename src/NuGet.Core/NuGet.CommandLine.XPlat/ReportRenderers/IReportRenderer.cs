// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.CommandLine.XPlat.ReportRenderers
{
    internal interface IReportRenderer
    {
        void WriteErrorLine(string errorText, string project);
        void Write(string value);
        void WriteLine();
        void WriteLine(string value);
        void Write(ReportProject reportProject);
        void SetForegroundColor(ConsoleColor consoleColor);
        void ResetColor();
        void LogParameters(string parameters);
        void End();
        int ExitCode();
    }
}
