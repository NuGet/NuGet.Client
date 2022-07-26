// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.CommandLine.XPlat.ReportRenderers
{
    internal class ConsoleWriter : IReportRenderer
    {
        public void ReportPayloadReceived(string payload)
        {
            Console.WriteLine(payload);
        }

        public void SetErrorText(string errorText)
        {
            Console.WriteLine(errorText);
        }

        private static ConsoleWriter _instance;

        public static ConsoleWriter Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ConsoleWriter();
                }

                return _instance;
            }
        }
    }
}
