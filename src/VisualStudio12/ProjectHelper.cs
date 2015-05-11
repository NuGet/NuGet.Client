// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using MsBuildProject = Microsoft.Build.Evaluation.Project;

#if VS12
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Designers;
using NuGet;
using System.Threading;
#endif

namespace NuGet.VisualStudio12
{
    public static class ProjectHelper
    {
#if VS12

    /// <summary>
    /// Performs an action inside a VS internal writer lock. If called from the UI thread this method will
    /// run async to avoid deadlocks.
    /// </summary>
        public static async Task DoWorkInWriterLockAsync(Project project, IVsHierarchy hierarchy, Action<MsBuildProject> action)
        {
            UnconfiguredProject unconfiguredProject = GetUnconfiguredProject((IVsProject)hierarchy);
            if (unconfiguredProject != null)
            {
                var service = unconfiguredProject.ProjectService.Services.ProjectLockService;
                if (service != null)
                {
                    // WriteLockAsync will move us to a background thread.
                    using (ProjectWriteLockReleaser x = await service.WriteLockAsync())
                    {
                        await x.CheckoutAsync(unconfiguredProject.FullPath);
                        ConfiguredProject configuredProject = await unconfiguredProject.GetSuggestedConfiguredProjectAsync();
                        MsBuildProject buildProject = await x.GetProjectAsync(configuredProject);

                        if (buildProject != null)
                        {
                            action(buildProject);
                        }

                        await x.ReleaseAsync();
                    }

                    // move to the UI thread for the rest of this method
                    await unconfiguredProject.ProjectService.Services.ThreadingPolicy.SwitchToUIThread();

                    project.Save();
                }
            }
        }

        private static void MakeFileWritable(string path)
        {
            FileAttributes attributes = File.GetAttributes(path);
            if (attributes.HasFlag(FileAttributes.ReadOnly))
            {
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            }
        }

        private static UnconfiguredProject GetUnconfiguredProject(IVsProject project)
        {
            IVsBrowseObjectContext context = project as IVsBrowseObjectContext;
            if (context == null)
            {
                IVsHierarchy hierarchy = project as IVsHierarchy;
                if (hierarchy != null)
                {
                    object extObject;
                    if (ErrorHandler.Succeeded(hierarchy.GetProperty((uint)VSConstants.VSITEMID.Root, (int)__VSHPROPID.VSHPROPID_ExtObject, out extObject)))
                    {
                        Project dteProject = extObject as Project;
                        if (dteProject != null)
                        {
                            context = dteProject.Object as IVsBrowseObjectContext;
                        }
                    }
                }
            }

            return context != null ? context.UnconfiguredProject : null;
        }
#else
        [SuppressMessage("Microsoft.Usage", "CA1801")]
        public static Task DoWorkInWriterLockAsync(Project project, IVsHierarchy hierarchy, Action<MsBuildProject> action)
        {
            return Task.FromResult(0);
        }
#endif
    }
}
