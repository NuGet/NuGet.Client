using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Test.Apex;
using Microsoft.Test.Apex.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.ServiceBroker;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Contracts;
using System.Linq;
using NuGet.Tests.Apex;

namespace NuGet
{
    [Export(typeof(TestIVsPackageSourceProvider))]
    public class TestIVsPackageSourceProvider : VisualStudioTestService
    {
        public IEnumerable<KeyValuePair<string, string>> GetSources(bool includeUnOfficial, bool includeDisabled)
        {
            IVsPackageSourceProvider packageSourceProvider = VisualStudioObjectProviders.GetComponentModelService<IVsPackageSourceProvider>();

            return packageSourceProvider.GetSources(includeUnOfficial, includeDisabled);
        }

        /// <summary>
        /// Gets the NuGet IVsPackageUninstaller
        /// </summary>
        protected internal IVsPackageUninstaller PackageUninstaller => VisualStudioObjectProviders.GetComponentModelService<IVsPackageUninstaller>();


        protected internal INuGetProjectService NuGetProjectService => VisualStudioObjectProviders.GetComponentModelService<INuGetProjectService>();

        /// <summary>
        /// Installs the specified NuGet package into the specified project
        /// </summary>
        /// <param name="project">Project name</param>
        /// <param name="packageName">NuGet package name</param>
        /// <param name="packageVersion">NuGet package version</param>
        public void InstallPackage(string projectName, string packageName, string packageVersion)
        {
            Logger.WriteMessage("Now installing NuGet package [{0} {1}] into project [{2}]", packageName, packageVersion, packageName);

            var unique = VisualStudioObjectProviders.DTE.Solution.Projects.Item(1).UniqueName;

            InstallPackage(null, unique, packageName, packageVersion);
        }

        /// <summary>
        /// Installs the specified NuGet package into the specified project
        /// </summary>
        /// <param name="source">Project source</param>
        /// <param name="project">Project name</param>
        /// <param name="packageName">NuGet package name</param>
        /// <param name="packageVersion">NuGet package version</param>
        public void InstallPackage(string source, string projectName, string packageName, string packageVersion)
        {
            Logger.WriteMessage("Now installing NuGet package [{0} {1} {2}] into project [{3}]", source, packageName, packageVersion, projectName);
            
            var projects = VisualStudioObjectProviders.DTE.Solution.Projects;
            var project = VisualStudioObjectProviders.DTE.Solution.Projects.Item(projectName);
            IVsPackageInstaller PackageInstaller = VisualStudioObjectProviders.GetComponentModelService<IVsPackageInstaller>();

            try
            {
                PackageInstaller.InstallPackage(source, project, packageName, packageVersion, false);
            }
            catch (InvalidOperationException e)
            {
                Logger.WriteException(EntryType.Warning, e, string.Format("An error occured while attempting to install package {0}", packageName));
            }
        }

        /// <summary>
        /// True if the package is installed based on the IVs APIs.
        /// </summary>
        public bool IsPackageInstalled(string packageName)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                var projectName = VisualStudioObjectProviders.DTE.Solution.Projects.Item(1).UniqueName;

                var package = await GetInstalledPackageAsync(projectName, packageName);
                return package != null;
            });
        }

        public async Task<NuGetInstalledPackage> GetInstalledPackageAsync(string projectName, string packageName)
        {
            var solution = VisualStudioObjectProviders.GetService<SVsSolution, IVsSolution>();
            int result = solution.GetProjectOfUniqueName(projectName, out IVsHierarchy project);
            if (result != VSConstants.S_OK)
            {
                throw new Exception($"Error calling {nameof(IVsSolution)}.{nameof(IVsSolution.GetProjectOfUniqueName)}: {result}");
            }


            result = solution.GetGuidOfProject(project, out Guid projectGuid);
            if (result != VSConstants.S_OK)
            {
                throw new Exception($"Error calling {nameof(IVsSolution)}.{nameof(IVsSolution.GetGuidOfProject)}: {result}");
            }

            var serviceBrokerContainer = VisualStudioObjectProviders.GetService<SVsBrokeredServiceContainer, IBrokeredServiceContainer>();
            var serviceBroker = serviceBrokerContainer.GetFullAccessServiceBroker();

#pragma warning disable ISB001 // Dispose of proxies
            var projectService = await serviceBroker.GetProxyAsync<INuGetProjectService>(NuGetServices.NuGetProjectServiceV1);
#pragma warning restore ISB001 // Dispose of proxies
            return null;
        }

        public void UninstallPackage(string projectName, string packageName)
        {
            var unique = VisualStudioObjectProviders.DTE.Solution.Projects.Item(1).UniqueName;
            UninstallPackage(unique, packageName, false);
        }

        /// <summary>
        /// Uninstalls the specified NuGet package from the project
        /// </summary>
        /// <param name="project">Project name</param>
        /// <param name="packageName">NuGet package name</param>
        /// <param name="removeDependencies">Whether to uninstall any package dependencies</param>
        public void UninstallPackage(string projectName, string packageName, bool removeDependencies)
        {
            Logger.WriteMessage("Now uninstalling NuGet package [{0}] from project [{1}]", packageName, projectName);
            
            var project = VisualStudioObjectProviders.DTE.Solution.Projects.Item(projectName);

            try
            {
                PackageUninstaller.UninstallPackage(project, packageName, removeDependencies);
            }
            catch (InvalidOperationException e)
            {
                Logger.WriteException(EntryType.Warning, e, string.Format("An error occured while attempting to uninstall package {0}", packageName));
            }
        }
    }
}
