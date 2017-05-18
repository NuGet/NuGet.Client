// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
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

        public bool TryCreateNuGetProject(
            IVsProjectAdapter project, 
            ProjectProviderContext context, 
            bool forceProjectType,
            out NuGetProject result)
        {
            Assumes.Present(project);
            Assumes.Present(context);

            result = null;

            ThreadHelper.ThrowIfNotOnUIThread();

            if (project.IsDeferred)
            {
                return false;
            }

            var projectK = EnvDTEProjectUtility.GetProjectKPackageManager(project.Project);
            if (projectK == null)
            {
                return false;
            }

            result = new ProjectKNuGetProject(
                projectK,
                project.ProjectName,
                project.CustomUniqueName,
                project.ProjectId);

            return true;
        }
    }
}
