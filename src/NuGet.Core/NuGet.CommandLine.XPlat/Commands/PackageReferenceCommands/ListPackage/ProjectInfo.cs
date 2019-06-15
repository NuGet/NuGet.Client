// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.ProjectModel;

namespace NuGet.CommandLine.XPlat
{
    /// <summary>
    /// A class to simplify holding all of the information
    /// about a project when using list
    /// </summary>
    internal class ProjectInfo
    {
        internal ProjectInfo(string projectName,
            string projectPath,
            string solutionDirectoryPath,
            ProjectStyle projectStyle)
        {
            ProjectName = projectName;
            ProjectPath = projectPath;
            ProjectStyle = projectStyle;
            SolutionDirectoryPath = solutionDirectoryPath;
            _targetFrameworkInfos = new List<TargetFrameworkInfo>();
        }

        internal string ProjectName { get; }
        internal string ProjectPath { get; }
        internal ProjectStyle ProjectStyle { get; }
        internal IEnumerable<TargetFrameworkInfo> TargetFrameworkInfos
        {
            get
            {
                return (IEnumerable<TargetFrameworkInfo>)_targetFrameworkInfos;
            }
        }
        internal string SolutionDirectoryPath { get; }

#if IS_CORECLR
        internal string ProjectDirectoryRelativePath
        {
            get
            {
                if (SolutionDirectoryPath == null)
                {
                    return null;
                }

                return Path.GetDirectoryName(ProjectPath).Substring(SolutionDirectoryPath.Length + 1);
            }
        }
#endif

        private List<TargetFrameworkInfo> _targetFrameworkInfos;

        internal void AddTargetFrameworkInfo(TargetFrameworkInfo targetFrameworkInfo)
        {
            _targetFrameworkInfos.Add(targetFrameworkInfo);
        }

        internal void AddTargetFrameworkInfos(IEnumerable<TargetFrameworkInfo> targetFrameworkInfos)
        {
            _targetFrameworkInfos.AddRange(targetFrameworkInfos);
        }
    }
}