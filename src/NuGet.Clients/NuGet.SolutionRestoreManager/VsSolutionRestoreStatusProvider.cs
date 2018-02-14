// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.Shared;
using NuGet.VisualStudio;

namespace NuGet.SolutionRestoreManager
{
    [PartCreationPolicy(CreationPolicy.Shared)]
    [Export(typeof(IVsSolutionRestoreStatusProvider))]
    public class VsSolutionRestoreStatusProvider : IVsSolutionRestoreStatusProvider
    {
        private readonly Lazy<ISolutionRestoreWorker> _restoreWorker;
        private readonly Lazy<IVsSolutionManager> _solutionManager;

        [ImportingConstructor]
        public VsSolutionRestoreStatusProvider(
            Lazy<ISolutionRestoreWorker> restoreWorker,
            Lazy<IVsSolutionManager> solutionManager)
        {
            _restoreWorker = restoreWorker;
            _solutionManager = solutionManager;
        }

        /// <summary>
        /// True if all projects have been nominated and the restore worker has completed all work.
        /// </summary>
        public async Task<bool> IsRestoreCompleteAsync(CancellationToken token)
        {
            var complete = true;

            // Check if the solution is open, if there are no projects then consider it restored.
            if (_solutionManager.Value.IsSolutionOpen)
            {
                var graphContext = new DependencyGraphCacheContext();
                var projects = (await _solutionManager.Value.GetNuGetProjectsAsync()).AsList();

                // Empty solutions with no projects are considered restored.
                foreach (var project in projects)
                {
                    token.ThrowIfCancellationRequested();

                    // Check if the project has a spec to see if nomination is complete.
                    complete &= await HasSpecAsync(project, graphContext);
                }

                // Check if the restore worker is currently active.
                complete &= !_restoreWorker.Value.IsRunning;
            }

            return complete;
        }

        /// <summary>
        /// True if the project has a spec available for restore.
        /// </summary>
        private static async Task<bool> HasSpecAsync(NuGetProject project, DependencyGraphCacheContext graphContext)
        {
            var buildProject = project as BuildIntegratedNuGetProject;

            if (buildProject != null)
            {
                try
                {
                    var specs = await buildProject.GetPackageSpecsAsync(graphContext);

                    if (specs?.Count < 1)
                    {
                        // Spec has not been loaded
                        return false;
                    }
                }
                catch (InvalidOperationException)
                {
                    // This is thrown if a project has not yet been nominated.
                    return false;
                }
            }

            return true;
        }
    }
}
