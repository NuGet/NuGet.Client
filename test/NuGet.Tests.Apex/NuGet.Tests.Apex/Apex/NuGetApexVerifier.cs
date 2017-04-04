using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.Test.Apex.VisualStudio;

namespace NuGet.Tests.Apex
{
    public class NuGetApexVerifier : VisualStudioMarshallableProxyVerifier
    {
        /// <summary>
        /// Gets the Nuget Package Manager test service
        /// </summary>
        private NuGetApexTestService NugetPackageManager
        {
            get { return (NuGetApexTestService)this.Owner; }
        }

        /// <summary>
        /// Validate whether a NuGet package is installed
        /// </summary>
        /// <param name="project">project name</param>
        /// <param name="packageName">NuGet package name</param>
        /// <returns>True if the package is installed; otherwise false</returns>
        public bool PackageIsInstalled(string projectName, string packageName)
        {
            var project = NugetPackageManager.Dte.Solution.Projects.Item(projectName);
            return this.IsTrue(this.NugetPackageManager.InstallerServices.IsPackageInstalled(project, packageName), "Expected NuGet package {0} to be installed in project {1}.", packageName, project.Name);
        }

        /// <summary>
        /// Validate whether a NuGet package is not installed
        /// </summary>
        /// <param name="project">Project name</param>
        /// <param name="packageName">NuGet package name</param>
        /// <returns>True if the package is not installed; otherwise false</returns>
        public bool PackageIsNotInstalled(string projectName, string packageName)
        {
            var project = NugetPackageManager.Dte.Solution.Projects.Item(projectName);
            return this.IsFalse(this.NugetPackageManager.InstallerServices.IsPackageInstalled(project, packageName), "Expected NuGet package {0} to not be installed in project {1}.", packageName, project.Name);
        }
    }
}
