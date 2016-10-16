using System;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    public static class ScriptExecutorUtil
    {
        public static async Task<bool> ExecuteScriptAsync(PackageIdentity identity,
            string packageInstallPath,
            INuGetProjectContext projectContext,
            IScriptExecutor scriptExecutor,
            EnvDTEProject envDTEProject,
            bool throwOnFailure)
        {
            if (scriptExecutor != null)
            {
                var packageReader = new PackageFolderReader(packageInstallPath);

                var toolItemGroups = packageReader.GetToolItems();

                if (toolItemGroups != null)
                {
                    // Init.ps1 must be found at the root folder, target frameworks are not recognized here,
                    // since this is run for the solution.
                    var toolItemGroup = toolItemGroups
                                        .Where(group => group.TargetFramework.IsAny)
                                        .FirstOrDefault();

                    if (toolItemGroup != null)
                    {
                        var initPS1RelativePath = toolItemGroup.Items
                            .FirstOrDefault(p => p.StartsWith(
                                PowerShellScripts.InitPS1RelativePath,
                                StringComparison.OrdinalIgnoreCase));

                        if (!string.IsNullOrEmpty(initPS1RelativePath))
                        {
                            initPS1RelativePath = PathUtility
                                .ReplaceAltDirSeparatorWithDirSeparator(initPS1RelativePath);

                            return await scriptExecutor.ExecuteAsync(
                                identity,
                                packageInstallPath,
                                initPS1RelativePath,
                                envDTEProject,
                                projectContext,
                                throwOnFailure);
                        }
                    }
                }
            }
            return false;
        }
    }
}
