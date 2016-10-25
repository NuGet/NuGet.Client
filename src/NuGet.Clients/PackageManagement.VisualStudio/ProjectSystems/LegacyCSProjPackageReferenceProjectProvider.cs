// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;
using NuGet.ProjectManagement;
using VSLangProj150;
using ProjectSystem = Microsoft.VisualStudio.ProjectSystem;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(IProjectSystemProvider))]
    [Name(nameof(LegacyCSProjPackageReferenceProjectProvider))]
    [Order(After = nameof(CpsPackageReferenceProjectProvider))]
    public class LegacyCSProjPackageReferenceProjectProvider : IProjectSystemProvider
    {
        private readonly IProjectSystemCache _projectSystemCache;

        [ImportingConstructor]
        public LegacyCSProjPackageReferenceProjectProvider(IProjectSystemCache projectSystemCache)
        {
            if (projectSystemCache == null)
            {
                throw new ArgumentNullException(nameof(projectSystemCache));
            }

            _projectSystemCache = projectSystemCache;
        }
        
        public bool TryCreateNuGetProject(EnvDTE.Project dteProject, ProjectSystemProviderContext context, out NuGetProject result)
        {
            if (dteProject == null)
            {
                throw new ArgumentNullException(nameof(dteProject));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            result = null;

            var project = new EnvDTEProjectAdapter(dteProject);
            if (!project.IsLegacyCSProjPackageReferenceProject)
            {
                return false;
            }

            result = new LegacyCSProjPackageReferenceProject(
                project,
                VsHierarchyUtility.GetProjectId(dteProject));

            return true;
        }

        private ProjectSystem.UnconfiguredProject GetUnconfiguredProject(EnvDTE.Project project)
        {
            IVsBrowseObjectContext context = project as IVsBrowseObjectContext;
            if (context == null && project != null)
            { // VC implements this on their DTE.Project.Object
                context = project.Object as IVsBrowseObjectContext;
            }

            return context != null ? context.UnconfiguredProject : null;
        }
    }
}
