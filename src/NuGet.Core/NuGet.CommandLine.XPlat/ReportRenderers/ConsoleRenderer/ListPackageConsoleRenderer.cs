// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.CommandLine.XPlat
{
    /// <summary>
    /// Console output renderer for dotnet list package command
    /// </summary>
    internal class ListPackageConsoleRenderer : IReportRenderer
    {
        protected List<ReportProblem> _problems = new();

        private ListPackageConsoleRenderer()
        { }

        public static ListPackageConsoleRenderer GetInstance()
        {
            return new ListPackageConsoleRenderer();
        }

        public void AddProblem(string errorText, ProblemType problemType)
        {
            _problems.Add(new ReportProblem(string.Empty, errorText, problemType));
        }

        public IEnumerable<ReportProblem> GetProblems()
        {
            return _problems;
        }

        public void AddToRenderer(ListPackageReportModel listPackageReportModel)
        {
            ListPackageConsoleWriter.Render(new ListPackageOutputContentV1()
            {
                ListPackageArgs = listPackageReportModel.ListPackageArgs,
                Problems = _problems,
                Projects = listPackageReportModel.Projects,
                AutoReferenceFound = listPackageReportModel.AutoReferenceFound
            });
        }
    }
}
