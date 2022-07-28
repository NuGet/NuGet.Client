// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.CommandLine.XPlat.ReportRenderers.ConsoleRenderers
{
    internal class ConsoleWriter : IReportRenderer
    {
        public void ReportPayloadReceived(string payload)
        {
            Console.WriteLine(payload);
        }

        public void SetErrorText(string errorText, string _)
        {
            Console.WriteLine(errorText);
        }

        public static ConsoleWriter Instance { get; } = new ConsoleWriter();
    }
}
