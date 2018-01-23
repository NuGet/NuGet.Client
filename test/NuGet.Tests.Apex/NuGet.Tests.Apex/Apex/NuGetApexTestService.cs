// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using EnvDTE;
using Microsoft.Test.Apex;
using Microsoft.Test.Apex.VisualStudio;
using Microsoft.Test.Apex.VisualStudio.Solution;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Console.TestContract;
using NuGet.PackageManagement.UI.TestContract;
using NuGet.VisualStudio;

namespace NuGet.Tests.Apex
{
    [Export(typeof(NuGetApexTestService))]
    public class NuGetApexTestService : VisualStudioTestService<NuGetApexVerifier>
    {
        [Import]
        private NuGetApexUITestService NuGetApexUITestService { get; set; }
        [Import]
        private NuGetApexConsoleTestService NuGetApexConsoleTestService { get; set; }

        public NuGetApexTestService()
        {
        }

        /// <summary>
        /// Gets the NuGet IVsPackageInstallerServices
        /// </summary>
        protected internal IVsPackageInstallerServices InstallerServices => VisualStudioObjectProviders.GetComponentModelService<IVsPackageInstallerServices>();

        /// <summary>
        /// Gets the NuGet IVsPackageInstaller
        /// </summary>
        protected internal IVsPackageInstaller PackageInstaller => VisualStudioObjectProviders.GetComponentModelService<IVsPackageInstaller>();

        protected internal DTE Dte => VisualStudioObjectProviders.DTE;


        /// <summary>
        /// Gets the NuGet IVsPackageUninstaller
        /// </summary>
        protected internal IVsPackageUninstaller PackageUninstaller => VisualStudioObjectProviders.GetComponentModelService<IVsPackageUninstaller>();

        protected internal IVsUIShell UIShell => VisualStudioObjectProviders.GetService<SVsUIShell, IVsUIShell>();

        /// <summary>
        /// Installs the specified NuGet package into the specified project
        /// </summary>
        /// <param name="project">Project name</param>
        /// <param name="packageName">NuGet package name</param>
        public void InstallPackage(string projectName, string packageName)
        {
            InstallPackage(projectName, packageName, null);
        }

        /// <summary>
        /// Installs the specified NuGet package into the specified project
        /// </summary>
        /// <param name="project">Project name</param>
        /// <param name="packageName">NuGet package name</param>
        /// <param name="packageVersion">NuGet package version</param>
        public void InstallPackage(string projectName, string packageName, string packageVersion)
        {
            Logger.WriteMessage("Now installing NuGet package [{0} {1}] into project [{2}]", packageName, packageVersion, packageName);

            InstallPackage(null, projectName, packageName, packageVersion);
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

            var project = Dte.Solution.Projects.Item(projectName);

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
        /// Uninstalls only the specified NuGet package from the project.
        /// </summary>
        /// <param name="project">Project name</param>
        /// <param name="packageName">NuGet package name</param>
        public void UninstallPackage(string projectName, string packageName)
        {
            UninstallPackage(projectName, packageName, false);
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

            var project = Dte.Solution.Projects.Item(projectName);

            try
            {
                PackageUninstaller.UninstallPackage(project, packageName, removeDependencies);
            }
            catch (InvalidOperationException e)
            {
                Logger.WriteException(EntryType.Warning, e, string.Format("An error occured while attempting to uninstall package {0}", packageName));
            }
        }

        public NuGetUIProjectTestExtension GetUIWindowfromProject(ProjectTestExtension project)
        {
            var uiproject = NuGetApexUITestService.GetApexTestUIProject(project.Name);
            return new NuGetUIProjectTestExtension(uiproject);
        }

        public NuGetConsoleTestExtension GetPackageManagerConsole(string project)
        {
            var pmconsole = NuGetApexConsoleTestService.GetApexTestConsole();
            return new NuGetConsoleTestExtension(pmconsole, project);
        }

        public bool EnsurePackageManagerConsoleIsOpen()
        {
            IVsWindowFrame window = null;
            var powerConsoleToolWindowGUID = new Guid("0AD07096-BBA9-4900-A651-0598D26F6D24");
            var stopwatch = Stopwatch.StartNew();
            var timeout = TimeSpan.FromMinutes(5);

            var found = UIShell.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fForceCreate, powerConsoleToolWindowGUID, out window);
            do
            {
                if (found == VSConstants.S_OK && window != null) {
                    window.Show();
                    return true;
                }
                found = UIShell.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fForceCreate, powerConsoleToolWindowGUID, out window);

                System.Threading.Thread.Sleep(100);
            }
            while (stopwatch.Elapsed < timeout);
            return false;
        }
    }
}
