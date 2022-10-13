// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.CommandLine.XPlat
{
    internal abstract class ListPackageJsonRenderer : IReportRenderer
    {
        protected readonly List<ReportProblem> _problems = new();
        protected ReportOutputVersion OutputVersion { get; private set; }

        protected ListPackageJsonRenderer(ReportOutputVersion outputVersion)
        {
            OutputVersion = outputVersion;
        }

        public void AddProblem(string errorText, ProblemType problemType)
        {
            _problems.Add(new ReportProblem(string.Empty, errorText, problemType));
        }

        public void End(ListPackageReportModel listPackageReportModel)
        {
            // Aggregate problems from projects.
            _problems.AddRange(listPackageReportModel.Projects.Where(p => p.ProjectProblems != null).SelectMany(p => p.ProjectProblems));
            string jsonRenderedOutput = ListPackageJsonOutputSerializerV1.Render(new ListPackageOutputContentV1()
            {
                ListPackageArgs = listPackageReportModel.ListPackageArgs,
                Problems = _problems,
                Projects = listPackageReportModel.Projects,
                AutoReferenceFound = listPackageReportModel.AutoReferenceFound
            });

            Console.WriteLine(jsonRenderedOutput);
        }
    }
}
