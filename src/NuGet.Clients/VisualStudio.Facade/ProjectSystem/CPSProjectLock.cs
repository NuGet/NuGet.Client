// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using MsBuildProject = Microsoft.Build.Evaluation.Project;
using Task = System.Threading.Tasks.Task;
using NuGet.Common;
using System.Threading;
using System.Threading.Tasks;
#if VS14
using Microsoft.VisualStudio.ProjectSystem.Designers;
#elif VS15
using Microsoft.VisualStudio.ProjectSystem.Properties;
#endif

namespace NuGet.VisualStudio.Facade.ProjectSystem
{
    internal class CPSProjectLock : INuGetLock
    {
        private readonly Project _project;
        private readonly IVsHierarchy _hierarchy;
        private readonly string _projectPath;

        public CPSProjectLock(Project project, IVsHierarchy hierarchy, string projectPath)
        {
            _project = project;
            _hierarchy = hierarchy;
            _projectPath = projectPath;
        }

        public string Id
        {
            get
            {
                return _projectPath;
            }
        }

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> asyncAction)
        {
            return await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                // Move to the UI thread to the get the lock
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var vsProject = (IVsProject)_hierarchy;
                UnconfiguredProject unconfiguredProject = ProjectHelper.GetUnconfiguredProject(vsProject);
                if (unconfiguredProject != null)
                {
                    // Get lock service
                    var service = unconfiguredProject.ProjectService.Services.ProjectLockService;
                    if (service != null)
                    {
                        using (ProjectWriteLockReleaser x = await service.WriteLockAsync())
                        {
                            // Run action within the lock
                            return await asyncAction();
                        }
                    }
                }

                // Run without the lock if the service cannot be acquired
                return await asyncAction();
            });
        }
    }
}
