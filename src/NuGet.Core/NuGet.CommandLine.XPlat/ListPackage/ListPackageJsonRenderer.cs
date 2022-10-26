// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace NuGet.CommandLine.XPlat.ListPackage
{
    /// <summary>
    /// json format renderer for dotnet list package command
    /// </summary>
    internal abstract class ListPackageJsonRenderer : IReportRenderer
    {
        protected readonly List<ReportProblem> _problems = new();
        protected TextWriter _writer;
        protected int ReportOutputVersion { get; private set; }

        private ListPackageJsonRenderer()
        { }

        protected ListPackageJsonRenderer(int reportOutputVersion, TextWriter textWriter)
        {
            ReportOutputVersion = reportOutputVersion;
            _writer = textWriter != null ? textWriter : Console.Out;
        }

        public void AddProblem(string errorText, ProblemType problemType)
        {
            _problems.Add(new ReportProblem(string.Empty, errorText, problemType));
        }

        public IEnumerable<ReportProblem> GetProblems()
        {
            return _problems;
        }

        public abstract void Render(ListPackageReportModel listPackageReportModel);
    }
}
