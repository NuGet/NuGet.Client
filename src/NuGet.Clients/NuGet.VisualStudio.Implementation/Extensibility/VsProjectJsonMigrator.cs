using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsProjectJsonMigrator))]
    class VsProjectJsonMigrator : IVsProjectJsonMigrator
    {
        private IVsSolutionManager _solutionManager;
        private PumpingJTF _pumpingJtf;

        [ImportingConstructor]
        public VsProjectJsonMigrator()
        {
            _solutionManager = ServiceLocator.GetInstance<IVsSolutionManager>();
            _pumpingJtf = new PumpingJTF(NuGetUIThreadHelper.JoinableTaskFactory.Context);
        }
        public IVsProjectJsonMigrateResult MigrateProjectToPackageRef(string projectUniqueName)
        {            
            return _pumpingJtf.Run(async delegate
            {
                return await MigrateProjectToPackageRefAsync(projectUniqueName);
            });
            
        }

        private async Task<IVsProjectJsonMigrateResult> MigrateProjectToPackageRefAsync(string projectUniqueName)
        {
            var vsSolMngr = _solutionManager as VSSolutionManager;
            var project = vsSolMngr.GetDTEProject(projectUniqueName);
            var projectSafeName = await EnvDTEProjectUtility.GetCustomUniqueNameAsync(project);
            var nuGetProject = vsSolMngr.GetNuGetProject(projectSafeName);
            try
            {
                var legacyPackageRefBasedProject = await vsSolMngr.UpdateNuGetProjectToPackageRef(nuGetProject) as LegacyCSProjPackageReferenceProject;
                var projectJsonMigrator = new ProjectJsonToPackageRefMigrator(legacyPackageRefBasedProject, project, VsHierarchyUtility.ToVsHierarchy(project));
                
                return new VsProjectJsonMigrateResult(await projectJsonMigrator.MigrateAsync());
            }
            catch(Exception ex)
            {
                // reload the project in memory from the file on disk, discarding any changes
                project = vsSolMngr.GetDTEProject(projectUniqueName);
                return await Task.FromResult(new VsProjectJsonMigrateResult(ex.Message));
            }            
        }
    }
}
