// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// This is an implementation of <see cref="MSBuildNuGetProject"/> that has knowledge about interacting with DTE.
    /// Since the base class <see cref="MSBuildNuGetProject"/> is in the NuGet.Core solution, it does not have
    /// references to DTE.
    /// </summary>
    public class VSMSBuildNuGetProject : MSBuildNuGetProject
    {
        private readonly EnvDTEProject _project;

        public VSMSBuildNuGetProject(
            EnvDTEProject project,
            IMSBuildNuGetProjectSystem msbuildNuGetProjectSystem,
            string folderNuGetProjectPath,
            string packagesConfigFolderPath,
            string projectId) : base(
                msbuildNuGetProjectSystem,
                folderNuGetProjectPath,
                packagesConfigFolderPath,
                projectId)
        {
            _project = project;
        }

        public override async Task<IReadOnlyList<ExternalProjectReference>> GetProjectReferenceClosureAsync(
            ExternalProjectReferenceContext context)
        {
            return await VSProjectReferenceUtility.GetProjectReferenceClosureAsync(_project, context);
        }
    }
}
