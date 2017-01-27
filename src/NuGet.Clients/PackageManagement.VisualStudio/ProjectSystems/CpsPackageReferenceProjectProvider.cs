// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;
using NuGet.PackageManagement.UI;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Provides a method of creating <see cref="CpsPackageReferenceProject"/> instance.
    /// </summary>
    [Export(typeof(IProjectSystemProvider))]
    [Name(nameof(CpsPackageReferenceProjectProvider))]
    [Microsoft.VisualStudio.Utilities.Order(After = nameof(ProjectKNuGetProjectProvider))]
    public class CpsPackageReferenceProjectProvider : IProjectSystemProvider
    {
        private readonly IProjectSystemCache _projectSystemCache;

        // Reason it's lazy<object> is because we don't want to load any CPS assemblies untill
        // we're really going to use any of CPS api. Which is why we also don't use nameof or typeof apis.
        [Import("Microsoft.VisualStudio.ProjectSystem.IProjectServiceAccessor")]
        private Lazy<object> ProjectServiceAccessor { get; set; }

        [ImportingConstructor]
        public CpsPackageReferenceProjectProvider(IProjectSystemCache projectSystemCache)
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

            // The project must be an IVsHierarchy.
            var hierarchy = VsHierarchyUtility.ToVsHierarchy(dteProject);
            
            if (hierarchy == null)
            {
                return false;
            }
            var restoreProjectStyle = string.Empty;
            var targetFramework = string.Empty;
            var targetFrameworks = string.Empty;

            var hasRestoreProjectStyle = context
                .MSBuildProperties
                .TryGetValue(ProjectSystemProviderContext.RestoreProjectStyle, out restoreProjectStyle) 
                && !string.IsNullOrEmpty(restoreProjectStyle);

            var hasTargetFramework = context
                .MSBuildProperties
                .TryGetValue(ProjectSystemProviderContext.TargetFramework, out targetFramework)
                && !string.IsNullOrEmpty(targetFramework);

            var hasTargetFrameworks = context
                .MSBuildProperties
                .TryGetValue(ProjectSystemProviderContext.TargetFrameworks, out targetFrameworks)
                && !string.IsNullOrEmpty(targetFrameworks);

            // check for RestoreProjectStyle property is set and if set to PackageReference then return false
            if (hasRestoreProjectStyle && !restoreProjectStyle.Equals(ProjectStyle.PackageReference.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            // Check if the project is not CPS capable or if it is CPS capable then it does not have TargetFramework(s), if so then return false
            else if (!(hierarchy.IsCapabilityMatch("CPS") && (hasTargetFramework || hasTargetFrameworks)))
            {
                return false;
            }

            // Lazy load the CPS enabled JoinableTaskFactory for the UI.
            NuGetUIThreadHelper.SetJoinableTaskFactoryFromService(ProjectServiceAccessor.Value as IProjectServiceAccessor);

            var projectNames = ProjectNames.FromDTEProject(dteProject);
            var fullProjectPath = EnvDTEProjectUtility.GetFullProjectPath(dteProject);
            var unconfiguredProject = GetUnconfiguredProject(dteProject);

            result = new CpsPackageReferenceProject(
                dteProject.Name,
                EnvDTEProjectUtility.GetCustomUniqueName(dteProject),
                fullProjectPath,
                _projectSystemCache,
                dteProject,
                unconfiguredProject,
                VsHierarchyUtility.GetProjectId(dteProject));

            return true;
        }

        private UnconfiguredProject GetUnconfiguredProject(EnvDTE.Project project)
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
