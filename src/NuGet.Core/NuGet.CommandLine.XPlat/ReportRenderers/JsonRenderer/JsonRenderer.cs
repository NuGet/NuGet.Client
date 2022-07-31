// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;

namespace NuGet.CommandLine.XPlat.ReportRenderers.JsonRenderer
{
    internal abstract class JsonRenderer : IReportRenderer
    {
        protected readonly List<ReportProblem> _problems = new();
        protected readonly List<string> _sources = new();
        protected readonly List<ReportProject> _projects = new();
        protected ReportOutputVersion OutputVersion { get; private set; }

        protected string _parameters = string.Empty;

        protected JsonRenderer(ReportOutputVersion outputVersion)
        {
            OutputVersion = outputVersion;
        }

        public void WriteErrorLine(string errorText, string project)
        {
            _problems.Add(new ReportProblem(project, errorText));
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

        public void LogParameters(string parameters)
        {
            if (string.IsNullOrEmpty(parameters))
            {
                _parameters = parameters;
            }
        }

        public void AddProjectData(ReportProject reportProject)
        {
            _projects.Add(reportProject);
        }

        public void FinishRendering()
        {
            try
            {
                string jsonRenderedOutput = JsonOutputFormat.Render(new JsonOutputContent()
                {
                    Parameters = _parameters,
                    Problems = _problems,
                    Projects = _projects,
                    Sources = _sources
                });

                Console.WriteLine(jsonRenderedOutput);
            }
            catch (Exception ex)
            {
                _problems.Add(new ReportProblem(string.Empty, ex.Message));
            }
        }

        public int ExitCode => _problems.Count > 0 ? 1 : 0;
    }
}
