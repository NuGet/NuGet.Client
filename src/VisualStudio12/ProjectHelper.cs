using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.Interop;
using MsBuildProject = Microsoft.Build.Evaluation.Project;
using EnvDTE;

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
        public static void DoWorkInWriterLock(Project project, IVsHierarchy hierarchy, Action<MsBuildProject> action)
        {
            // Perform this work on a new thread to avoid moving our current thread, and so it can be done async if needed.
            var task = Task.Run(() => DoWorkInWriterLockInternal(project, hierarchy, action));

            // Check if we are running on the UI thread. If we are we cannot risk blocking and holding the lock.
            // Ideally all calls involving the lock should be done on a background thread from the start to
            // keep the call as synchronous as possible within NuGet.
            if (!Microsoft.VisualStudio.Shell.ThreadHelper.CheckAccess())
            {
                // If we are on a background thread we can safely run synchronously.
                task.Wait();
            }
        }

        private static async Task DoWorkInWriterLockInternal(Project project, IVsHierarchy hierarchy, Action<MsBuildProject> action)
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

                    // perform the save synchronously
                    await Task.Run(() =>
                    {
                        // move to the UI thread for the rest of this method
                        unconfiguredProject.ProjectService.Services.ThreadingPolicy.SwitchToUIThread();

                        //var fileSystem = new PhysicalFileSystem(@"c:\");
                        //fileSystem.MakeFileWritable(project.FullName);
                        project.Save();
                    }
                    );
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
        public static void DoWorkInWriterLock(Project project, IVsHierarchy hierarchy, Action<MsBuildProject> action)
        {
        }
#endif
    }
}