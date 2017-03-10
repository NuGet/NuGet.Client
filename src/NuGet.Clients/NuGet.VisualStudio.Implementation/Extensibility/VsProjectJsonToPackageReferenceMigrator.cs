// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio.Implementation.Resources;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsProjectJsonToPackageReferenceMigrator))]
    internal class VsProjectJsonToPackageReferenceMigrator : IVsProjectJsonToPackageReferenceMigrator
    {
        private readonly Lazy<IVsSolutionManager> _solutionManager;

        [ImportingConstructor]
        public VsProjectJsonToPackageReferenceMigrator(Lazy<IVsSolutionManager> solutionManager)
        {
            _solutionManager = solutionManager;
            if(solutionManager == null)
            {
                throw new ArgumentNullException(nameof(solutionManager));
            }
        }
        public IVsProjectJsonToPackageReferenceMigrateResult MigrateProjectJsonToPackageReference(string projectUniqueName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if(string.IsNullOrEmpty(projectUniqueName))
            {
                throw new ArgumentNullException(nameof(projectUniqueName));
            }

            if(!File.Exists(projectUniqueName))
            {
                throw new FileNotFoundException(nameof(projectUniqueName));
            }

            return  NuGetUIThreadHelper.JoinableTaskFactory.Run(async () => 
            {
                await TaskScheduler.Default;
                return await MigrateProjectToPackageRefAsync(projectUniqueName);
            });
            
        }

        private async Task<IVsProjectJsonToPackageReferenceMigrateResult> MigrateProjectToPackageRefAsync(string projectUniqueName)
        {            
            var project = _solutionManager.Value.GetDTEProject(projectUniqueName);

            if (project == null)
            {
                throw new InvalidOperationException(string.Format(VsResources.Error_ProjectNotInCache, projectUniqueName));
            }

            var projectSafeName = await EnvDTEProjectUtility.GetCustomUniqueNameAsync(project);

            var nuGetProject = _solutionManager.Value.GetNuGetProject(projectSafeName);
            try
            {
                var legacyPackageRefBasedProject = await
                    _solutionManager.Value.UpdateNuGetProjectToPackageRef(nuGetProject) as LegacyCSProjPackageReferenceProject;

                await ProjectJsonToPackageRefMigrator.MigrateAsync(
                        legacyPackageRefBasedProject,
                        legacyPackageRefBasedProject.MSBuildProjectPath);
                var result = new VsProjectJsonToPackageReferenceMigrateResult(success: true, errorMessage: null);
                _solutionManager.Value.SaveProject(nuGetProject);

                return result;
            }
            catch(Exception ex)
            {
                // reload the project in memory from the file on disk, discarding any changes
                ReloadProject(project);
                return new VsProjectJsonToPackageReferenceMigrateResult(success: false , errorMessage: ex.Message);
            }            
        }

        private void ReloadProject(Project project)
        {
            project = _solutionManager.Value.GetDTEProject(project.FullName);
        }
    }
}
