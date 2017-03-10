// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using EnvDTE;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsProjectJsonMigrator))]
    internal class VsProjectJsonMigrator : IVsProjectJsonMigrator
    {
        private readonly IVsSolutionManager _solutionManager;

        [ImportingConstructor]
        public VsProjectJsonMigrator(IVsSolutionManager solutionManager)
        {
            _solutionManager = solutionManager;
        }
        public IVsProjectJsonMigrateResult MigrateProjectToPackageRef(string projectUniqueName)
        {
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
                return await MigrateProjectToPackageRefAsync(projectUniqueName);
            });
            
        }

        private async Task<IVsProjectJsonMigrateResult> MigrateProjectToPackageRefAsync(string projectUniqueName)
        {
            var project = _solutionManager.GetDTEProject(projectUniqueName);

            if (project == null)
            {
                throw new InvalidOperationException(string.Format("Project {0} does not exist in the project system cache.", projectUniqueName));
            }

            var projectSafeName = await EnvDTEProjectUtility.GetCustomUniqueNameAsync(project);
            var nuGetProject = _solutionManager.GetNuGetProject(projectSafeName);
            try
            {
                var legacyPackageRefBasedProject = await 
                    _solutionManager.UpdateNuGetProjectToPackageRef(nuGetProject) as LegacyCSProjPackageReferenceProject;

                var projectJsonMigrator = new ProjectJsonToPackageRefMigrator(
                    legacyPackageRefBasedProject, 
                    project);
                
                return new VsProjectJsonMigrateResult(await projectJsonMigrator.MigrateAsync());
            }
            catch(Exception ex)
            {
                // reload the project in memory from the file on disk, discarding any changes
                ReloadProject(project);
                return new VsProjectJsonMigrateResult(ex.Message);
            }            
        }

        private void ReloadProject(Project project)
        {
            project = _solutionManager.GetDTEProject(project.FullName);
        }
    }
}
