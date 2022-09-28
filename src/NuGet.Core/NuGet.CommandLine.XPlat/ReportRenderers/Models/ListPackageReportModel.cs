// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGet.CommandLine.XPlat.ReportRenderers.ConsoleRenderer;

namespace NuGet.CommandLine.XPlat.ReportRenderers.Models
{
    internal class ListPackageReportModel
    {
        internal List<string> Errors { get; } = new();
        internal ListPackageArgs ListPackageArgs { get; }
        internal List<ListPackageProjectDetails> Projects { get; } = new();

        internal MSBuildAPIUtility MSBuildAPIUtility { get; }

        private ListPackageReportModel()
        { }

        public ListPackageReportModel(ListPackageArgs listPackageArgs)
        {
            ListPackageArgs = listPackageArgs;
            MSBuildAPIUtility = new MSBuildAPIUtility(listPackageArgs.Logger);
        }

        public ListPackageProjectDetails CreateProjectReportData(string projectPath)
        {
            var projectModel = new ListPackageProjectDetails(projectPath, this);
            Projects.Add(projectModel);
            return projectModel;
        }

        public void AddError(string error)
        {
            Errors.Add(error);
        }

        //public string GetReportHeader()
        //{
        //    return string.Empty;
        //}
    }
}
