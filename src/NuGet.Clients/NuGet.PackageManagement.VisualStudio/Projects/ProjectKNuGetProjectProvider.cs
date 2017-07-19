// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(INuGetProjectProvider))]
    [Name(nameof(ProjectKNuGetProjectProvider))]
    public class ProjectKNuGetProjectProvider : INuGetProjectProvider
    {
        public RuntimeTypeHandle ProjectType => typeof(ProjectKNuGetProject).TypeHandle;

        public async Task<NuGetProject> TryCreateNuGetProjectAsync(
            IVsProjectAdapter project, 
            ProjectProviderContext context, 
            bool forceProjectType)
        {
            Assumes.Present(project);
            Assumes.Present(context);

            ThreadHelper.ThrowIfNotOnUIThread();

            if (project.IsDeferred)
            {
                return null;
            }

            var projectK = EnvDTEProjectUtility.GetProjectKPackageManager(project.Project);
            if (projectK == null)
            {
                return null;
            }

            return await System.Threading.Tasks.Task.FromResult(new ProjectKNuGetProject(
                projectK,
                project.ProjectName,
                project.CustomUniqueName,
                project.ProjectId));
        }
    }
}
