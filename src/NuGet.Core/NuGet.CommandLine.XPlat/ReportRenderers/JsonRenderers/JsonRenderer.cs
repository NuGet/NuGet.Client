// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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

        public void SetErrorText(string errorText, string project)
        {
            _problems.Add(new RenderProblem(project, errorText));
        }

        public virtual void WriteResult()
        {
            System.Console.WriteLine("Write Json");
        }
    }
}
