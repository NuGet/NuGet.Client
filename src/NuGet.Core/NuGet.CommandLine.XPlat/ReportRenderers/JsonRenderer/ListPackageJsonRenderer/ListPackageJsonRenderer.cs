// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NuGet.CommandLine.XPlat
{
    internal abstract class ListPackageJsonRenderer : IReportRenderer
    {
        protected readonly List<ReportProblem> _problems = new();
        protected ReportOutputVersion OutputVersion { get; private set; }
        private TextWriter _writer;

        private ListPackageJsonRenderer()
        { }

        protected ListPackageJsonRenderer(ReportOutputVersion outputVersion, TextWriter textWriter)
        {
            OutputVersion = outputVersion;
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

        public void End(ListPackageReportModel listPackageReportModel)
        {
            // Aggregate problems from projects.
            _problems.AddRange(listPackageReportModel.Projects.Where(p => p.ProjectProblems != null).SelectMany(p => p.ProjectProblems).Where(p => p.ProblemType != ProblemType.Information));
            string jsonRenderedOutput = ListPackageJsonOutputSerializerV1.Render(new ListPackageOutputContentV1()
            {
                ListPackageArgs = listPackageReportModel.ListPackageArgs,
                Problems = _problems,
                Projects = listPackageReportModel.Projects,
                AutoReferenceFound = listPackageReportModel.AutoReferenceFound
            });

            _writer.WriteLine(jsonRenderedOutput);
        }
    }
}
