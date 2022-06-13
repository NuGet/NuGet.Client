// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.VCProjectEngine;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Reference reader implementation for the core project system in the integrated development environment (IDE).
    /// </summary>
    internal class NativeProjectSystemReferencesReader
        : IProjectSystemReferencesReader
    {
        private readonly IVsProjectAdapter _vsProjectAdapter;
        private readonly IVsProjectThreadingService _threadingService;
        private readonly AsyncLazy<VCProject> _vcProject;

        public NativeProjectSystemReferencesReader(
            IVsProjectAdapter vsProjectAdapter,
            IVsProjectThreadingService threadingService)
        {
            Assumes.Present(vsProjectAdapter);
            Assumes.Present(threadingService);

            _vsProjectAdapter = vsProjectAdapter;
            _threadingService = threadingService;
            _vcProject = new AsyncLazy<VCProject>(async () =>
            {
                await threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();
                return _vsProjectAdapter.Project.Object as VCProject;
            }, threadingService.JoinableTaskFactory);
        }

        public async Task<IEnumerable<ProjectRestoreReference>> GetProjectReferencesAsync(
            Common.ILogger logger, CancellationToken _)
        {
            var results = new List<ProjectRestoreReference>();

            await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();
            var vcProject = await _vcProject.GetValueAsync();
            var references = vcProject.VCReferences as VCReferences;
            var projectReferences = references.GetReferencesOfType((uint)vcRefType.VCRT_PROJECT);

            foreach (var reference in projectReferences)
            {
                var vcReference = reference as VCReference;
                if (vcReference.UseInBuild)
                {
                    var childProjectPath = vcReference.FullPath;
                    var projectRestoreReference = new ProjectRestoreReference()
                    {
                        ProjectPath = childProjectPath,
                        ProjectUniqueName = childProjectPath
                    };

                    results.Add(projectRestoreReference);
                }
            }
            return results;
        }

        public Task<IEnumerable<LibraryDependency>> GetPackageReferencesAsync(
            NuGetFramework targetFramework, CancellationToken _)
        {
            throw new NotSupportedException();
        }
    }
}
