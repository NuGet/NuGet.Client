// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal interface IMSBuildAPIUtility
    {
        public ILogger Logger { get; }

        /// <summary>
        /// Opens an MSBuild.Evaluation.Project type from a csproj file.
        /// </summary>
        /// <param name="projectCSProjPath">CSProj file which needs to be evaluated</param>
        /// <returns>MSBuild.Evaluation.Project</returns>
        Project GetProject(string projectCSProjPath);

        /// <summary>
        /// Enumerates projects within a solution
        /// </summary>
        /// <param name="solutionPath">Solution file whose projects need to be enumerated</param>
        IEnumerable<string> GetProjectsFromSolution(string solutionPath);

        /// <summary>
        /// A simple check for some of the evaluated properties to check
        /// if the project is package reference project or not
        /// </summary>
        /// <param name="project"></param>
        /// <returns></returns>
        bool IsPackageReferenceProject(Project project);

        /// <summary>
        /// Prepares the dictionary that maps frameworks to packages top-level
        /// and transitive.
        /// </summary>
        /// <param name="projectPath"> Path to the project to get versions for its packages </param>
        /// <param name="userInputFrameworks">A list of frameworks</param>
        /// <param name="assetsFile">Assets file for all targets and libraries</param>
        /// <param name="transitive">Include transitive packages/projects in the result</param>
        /// <param name="includeProjects">Include project references in top-level and transitive package lists</param>
        /// <returns>FrameworkPackages collection with top-level and transitive package/project
        /// references for each framework, or null on error</returns>
        IEnumerable<FrameworkPackages> GetResolvedVersions(
            string projectPath, IEnumerable<string> userInputFrameworks, LockFile assetsFile, bool transitive, bool includeProjects);
     }
}
