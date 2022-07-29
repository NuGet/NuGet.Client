// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.CommandLine.XPlat.ReportRenderers
{
    internal interface IReportRenderer
    {
        void WriteErrorLine(string errorText, string project);
        void WriteLine();
        void WriteLine(string value);
    }
}
