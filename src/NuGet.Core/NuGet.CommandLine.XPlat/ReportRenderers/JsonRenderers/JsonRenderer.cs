// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.CommandLine.XPlat.ReportRenderers.JsonRenderers
{
    internal abstract class JsonRenderer : IReportRenderer
    {
        protected readonly List<RenderProblem> _problems = new();
        protected readonly List<string> _sources = new();
        protected readonly List<ReportProject> _projects = new();
        protected OutputVersion OutputVersion { get; private set; }

        protected JsonRenderer(OutputVersion outputVersion)
        {
            OutputVersion = outputVersion;
        }

        public void WriteErrorLine(string errorText, string project)
        {
            _problems.Add(new RenderProblem(project, errorText));
        }

        public virtual void WriteResult()
        {
            JsonOutputFormat.Render(new JsonOutputContent()
            {
                Parameters = "parameters",
                Problems = _problems,
                Projects = _projects,
                Sources = _sources
            });
        }

        public void Write(string value)
        {
            // do nothing
        }

        public void WriteLine()
        {
            // do nothing
        }

        public void WriteLine(string value)
        {
            // do nothing
        }

        public void SetForegroundColor(ConsoleColor consoleColor)
        {
            // do nothing
        }

        public void ResetColor()
        {
            // do nothing
        }
    }
}
