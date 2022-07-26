// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.CommandLine.XPlat.ReportRenderers
{
    internal class JsonRenderer : IReportRenderer
    {
        private OutputVersion _outputVersion = OutputVersion.V1;
        private readonly List<string> _problems = new();

        public JsonRenderer()
        {
            if (_outputVersion == OutputVersion.V1)
            {
                Console.WriteLine("v1");
            }
            else
            {
                Console.WriteLine("Unsupported version");
            }

        }

        public void ReportPayloadReceived(string payload)
        {
        }

        public void SetErrorText(string errorText)
        {
            _problems.Add(errorText);
        }
    }
}
