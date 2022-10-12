// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.CommandLine.XPlat.ReportRenderers.Models
{
    internal class ListPackageReportModel
    {
        internal ListPackageArgs ListPackageArgs { get; }
        internal List<ListPackageProjectModel> Projects { get; } = new();

        internal MSBuildAPIUtility MSBuildAPIUtility { get; }

        private ListPackageReportModel()
        { }

        internal ListPackageReportModel(ListPackageArgs listPackageArgs)
        {
            ListPackageArgs = listPackageArgs;
            MSBuildAPIUtility = new MSBuildAPIUtility(listPackageArgs.Logger);
        }

        internal ListPackageProjectModel CreateProjectReportData(string projectPath)
        {
            var projectModel = new ListPackageProjectModel(projectPath, this);
            Projects.Add(projectModel);
            return projectModel;
        }
    }
}
