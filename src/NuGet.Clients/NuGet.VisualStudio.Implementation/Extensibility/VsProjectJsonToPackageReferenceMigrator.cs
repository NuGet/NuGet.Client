// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.VisualStudio.Implementation.Resources;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsProjectJsonToPackageReferenceMigrator))]
    internal class VsProjectJsonToPackageReferenceMigrator : IVsProjectJsonToPackageReferenceMigrator
    {
        private readonly Lazy<IVsSolutionManager> _solutionManager;
        private readonly Lazy<NuGetProjectFactory> _projectFactory;

        [ImportingConstructor]
        public VsProjectJsonToPackageReferenceMigrator(
            Lazy<IVsSolutionManager> solutionManager,
            Lazy<NuGetProjectFactory> projectFactory)
        {
            Assumes.Present(solutionManager);
            Assumes.Present(projectFactory);

            _solutionManager = solutionManager;
            _projectFactory = projectFactory;
        }

        public Task<object> MigrateProjectJsonToPackageReferenceAsync(string projectUniqueName)
        {
            if (string.IsNullOrEmpty(projectUniqueName))
            {
                throw new ArgumentNullException(nameof(projectUniqueName));
            }

            if (!File.Exists(projectUniqueName))
            {
                throw new FileNotFoundException(string.Format(VsResources.Error_FileNotExists, projectUniqueName));
            }

            return MigrateProjectToPackageRefAsync(projectUniqueName);
        }

        private async Task<object> MigrateProjectToPackageRefAsync(string projectUniqueName)
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var project = await _solutionManager.Value.GetVsProjectAdapterAsync(projectUniqueName);

            if (project == null)
            {
                throw new InvalidOperationException(string.Format(VsResources.Error_ProjectNotInCache, projectUniqueName));
            }

            var projectSafeName = project.CustomUniqueName;

            var nuGetProject = await _solutionManager.Value.GetNuGetProjectAsync(projectSafeName);

            // If the project already has PackageReference, do nothing.
            if (nuGetProject is LegacyPackageReferenceProject)
            {
                return new VsProjectJsonToPackageReferenceMigrateResult(success: true, errorMessage: null);
            }

            try
            {
                await nuGetProject.SaveAsync(CancellationToken.None);

                var legacyPackageRefBasedProject = await _projectFactory.Value
                    .CreateNuGetProjectAsync<LegacyPackageReferenceProject>(
                        project, optionalContext: null);
                Assumes.Present(legacyPackageRefBasedProject);
                
                await ProjectJsonToPackageRefMigrator.MigrateAsync(
                    legacyPackageRefBasedProject as BuildIntegratedNuGetProject);
                var result = new VsProjectJsonToPackageReferenceMigrateResult(success: true, errorMessage: null);
                await nuGetProject.SaveAsync(CancellationToken.None);
                await _solutionManager.Value.UpgradeProjectToPackageReferenceAsync(nuGetProject);

                return result;

            }
            catch (Exception ex)
            {
                // reload the project in memory from the file on disk, discarding any changes that might have
                // been made as a result of an incomplete migration.
                await ReloadProjectAsync(project);
                return new VsProjectJsonToPackageReferenceMigrateResult(success: false, errorMessage: ex.Message);
            }
        }

        private async Task ReloadProjectAsync(IVsProjectAdapter project)
        {
            project = await _solutionManager.Value.GetVsProjectAdapterAsync(project.FullName);
        }
    }
}

