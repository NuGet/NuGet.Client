// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.CommandLine.XPlat.ReportRenderers.JsonRenderers
{
    internal abstract class JsonRenderer : IReportRenderer
    {
        protected readonly List<RenderProblem> _problems = new();
        protected OutputVersion OutputVersion { get; private set; }

        protected JsonRenderer(OutputVersion outputVersion)
        {
            OutputVersion = outputVersion;
        }

        public void ReportPayloadReceived(string payload)
        {
        }

        public void WriteErrorLine(string errorText, string project)
        {
            _problems.Add(new RenderProblem(project, errorText));
        }

        public virtual void WriteResult()
        {
            Console.WriteLine("Write Json");
        }

        public void WriteLine()
        {
            Console.WriteLine();
        }

        public void WriteLine(string value)
        {
            Console.WriteLine(value);
        }
    }
}
