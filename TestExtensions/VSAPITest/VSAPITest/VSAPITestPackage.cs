using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using Microsoft.Win32;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using NuGet.VisualStudio;
using System.Linq;
using System.Threading;

namespace MicrosoftCorp.VSAPITest
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.
    [PackageRegistration(UseManagedResourcesOnly = true)]
    // This attribute is used to register the information needed to show this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(GuidList.guidVSAPITestPkgString)]
    public sealed class VSAPITestPackage : Package
    {
        /// <summary>
        /// Default constructor of the package.
        /// Inside this method you can place any initialization code that does not require 
        /// any Visual Studio service because at this point the package object is created but 
        /// not sited yet inside Visual Studio environment. The place to do all the other 
        /// initialization is the Initialize method.
        /// </summary>
        public VSAPITestPackage()
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));
        }



        /////////////////////////////////////////////////////////////////////////////
        // Overridden Package Implementation
        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            Debug.WriteLine (string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
            base.Initialize();

            // Add our command handlers for menu (commands must exist in the .vsct file)
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if ( null != mcs )
            {
                // Create the command for the menu item.
                CommandID menuCommandID = new CommandID(GuidList.guidVSAPITestCmdSet, (int)PkgCmdIDList.cmdidNuGetAPITest);
                MenuCommand menuItem = new MenuCommand(GetInstalledPackagesTest, menuCommandID );
                mcs.AddCommand( menuItem );

                CommandID installPackageId = new CommandID(GuidList.guidVSAPITestCmdSet, (int)PkgCmdIDList.cmdidNuGetAPIInstallPackage);
                MenuCommand installPackageITem = new MenuCommand(InstallPackageTest, installPackageId);
                mcs.AddCommand(installPackageITem);

                CommandID installBadSrcId = new CommandID(GuidList.guidVSAPITestCmdSet, (int)PkgCmdIDList.cmdidNuGetAPIInstallBadSource);
                MenuCommand installBadSrcItem = new MenuCommand(InstallBadSourceTest, installBadSrcId);
                mcs.AddCommand(installBadSrcItem);

                CommandID installAsyncId = new CommandID(GuidList.guidVSAPITestCmdSet, (int)PkgCmdIDList.cmdidNuGetAPIInstallPackageAsync);
                MenuCommand installAsyncItem = new MenuCommand(InstallPackageAsyncTest, installAsyncId);
                mcs.AddCommand(installAsyncItem);

                CommandID sourcesId = new CommandID(GuidList.guidVSAPITestCmdSet, (int)PkgCmdIDList.cmdidNuGetAPIGetSources);
                MenuCommand sourcesItem = new MenuCommand(GetSourcesTest, sourcesId);
                mcs.AddCommand(sourcesItem);

                CommandID officialId = new CommandID(GuidList.guidVSAPITestCmdSet, (int)PkgCmdIDList.cmdidNuGetAPIGetOfficialSources);
                MenuCommand officialItem = new MenuCommand(GetOfficialSourcesTest, officialId);
                mcs.AddCommand(officialItem);

                CommandID emptyId = new CommandID(GuidList.guidVSAPITestCmdSet, (int)PkgCmdIDList.cmdidNuGetAPIInstallPackageEmptyVersion);
                MenuCommand emptyItem = new MenuCommand(InstallPackageEmptyVersionTest, emptyId);
                mcs.AddCommand(emptyItem);

                CommandID checkId = new CommandID(GuidList.guidVSAPITestCmdSet, (int)PkgCmdIDList.cmdidNuGetAPICheck);
                MenuCommand checkItem = new MenuCommand(CheckResult, checkId);
                mcs.AddCommand(checkItem);

                //cmdidNuGetAPIUninstallPackage
                CommandID uninstallId = new CommandID(GuidList.guidVSAPITestCmdSet, (int)PkgCmdIDList.cmdidNuGetAPIUninstallPackage);
                MenuCommand uninstallItem = new MenuCommand(UninstallPackage, uninstallId);
                mcs.AddCommand(uninstallItem);

                //cmdidNuGetAPIUninstallPackageNoDep
                CommandID uninstallNoDepId = new CommandID(GuidList.guidVSAPITestCmdSet, (int)PkgCmdIDList.cmdidNuGetAPIUninstallPackageNoDep);
                MenuCommand uninstallNoDepItem = new MenuCommand(UninstallPackageNoDep, uninstallNoDepId);
                mcs.AddCommand(uninstallNoDepItem);

                //cmdidNuGetAPIUninstallPackageNoForce
                CommandID uninstallNoForceId = new CommandID(GuidList.guidVSAPITestCmdSet, (int)PkgCmdIDList.cmdidNuGetAPIUninstallPackageNoForce);
                MenuCommand uninstallNoForceItem = new MenuCommand(UninstallPackageNoForce, uninstallNoForceId);
                mcs.AddCommand(uninstallNoForceItem);

                // cmdidNuGetAPIInstallPackageNoSource
                CommandID installNoSrcId = new CommandID(GuidList.guidVSAPITestCmdSet, (int)PkgCmdIDList.cmdidNuGetAPIInstallPackageNoSource);
                MenuCommand installNoSrcItem = new MenuCommand(InstallNoSourceTest, installNoSrcId);
                mcs.AddCommand(installNoSrcItem);
            }
        }
        #endregion

        private System.Threading.Tasks.Task _task;

        private void CheckResult(object sender, EventArgs e)
        {
            if (_task != null)
            {
                try
                {
                    _task.Wait();
                }
                catch (Exception ex)
                {
                    DisplayMessage("check", ex.InnerException.ToString());
                }

                DisplayMessage("check", _task.Status.ToString());
            }
        }

        private void UninstallPackage(object sender, EventArgs e)
        {
            EnvDTE.DTE dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
            IVsPackageInstaller services = ServiceLocator.GetInstance<IVsPackageInstaller>();
            IVsPackageUninstaller uninstaller = ServiceLocator.GetInstance<IVsPackageUninstaller>();

            foreach (EnvDTE.Project project in dte.Solution.Projects)
            {
                _task = System.Threading.Tasks.Task.Run(() =>
                {
                    services.InstallPackage("https://api.nuget.org/v2/", project, "windowsazure.storage", "4.3.0", false);
                    uninstaller.UninstallPackage(project, "windowsazure.storage", true);
                });

                return;
            }
        }

        private void UninstallPackageNoDep(object sender, EventArgs e)
        {
            EnvDTE.DTE dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
            IVsPackageInstaller services = ServiceLocator.GetInstance<IVsPackageInstaller>();
            IVsPackageUninstaller uninstaller = ServiceLocator.GetInstance<IVsPackageUninstaller>();

            foreach (EnvDTE.Project project in dte.Solution.Projects)
            {
                _task = System.Threading.Tasks.Task.Run(() =>
                {
                    services.InstallPackage("https://api.nuget.org/v2/", project, "windowsazure.storage", "4.3.0", false);
                    uninstaller.UninstallPackage(project, "windowsazure.storage", false);
                });
                return;
            }
        }

        private void UninstallPackageNoForce(object sender, EventArgs e)
        {
            EnvDTE.DTE dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
            IVsPackageInstaller services = ServiceLocator.GetInstance<IVsPackageInstaller>();
            IVsPackageUninstaller uninstaller = ServiceLocator.GetInstance<IVsPackageUninstaller>();

            foreach (EnvDTE.Project project in dte.Solution.Projects)
            {
                _task = System.Threading.Tasks.Task.Run(() =>
                {
                    services.InstallPackage("https://api.nuget.org/v2/", project, "windowsazure.storage", "4.3.0", false);
                    uninstaller.UninstallPackage(project, "newtonsoft.json", true);
                });
                return;
            }
        }

        private void GetOfficialSourcesTest(object sender, EventArgs e)
        {
            EnvDTE.DTE dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
            IVsPackageSourceProvider sourceProvider = ServiceLocator.GetInstance<IVsPackageSourceProvider>() as IVsPackageSourceProvider;

            foreach (EnvDTE.Project project in dte.Solution.Projects)
            {
                var sources = sourceProvider.GetSources(false, false);
                DisplayMessage("Official enabled sources", String.Join(", ", sources.Select(p => String.Format("[{0} {1}]", p.Key, p.Value))));
                return;
            }
        }

        private void GetSourcesTest(object sender, EventArgs e)
        {
            EnvDTE.DTE dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
            IVsPackageSourceProvider sourceProvider = ServiceLocator.GetInstance<IVsPackageSourceProvider>() as IVsPackageSourceProvider;

            foreach (EnvDTE.Project project in dte.Solution.Projects)
            {
                var sources = sourceProvider.GetSources(true, true);
                DisplayMessage("All sources", String.Join(", ", sources.Select(p => String.Format("[{0} {1}]", p.Key, p.Value))));
                return;
            }
        }

        private void InstallPackageAsyncTest(object sender, EventArgs e)
        {
            EnvDTE.DTE dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
            IVsPackageInstaller2 installer2 = ServiceLocator.GetInstance<IVsPackageInstaller>() as IVsPackageInstaller2;

            foreach (EnvDTE.Project project in dte.Solution.Projects)
            {
                _task = installer2.InstallPackageAsync(project, new string[] { "https://api.nuget.org/v2/" }, "dotnetrdf", "[1.0.0, )", false, CancellationToken.None);
                return;
            }
        }

        private void InstallPackageTest(object sender, EventArgs e)
        {
            EnvDTE.DTE dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
            IVsPackageInstaller services = ServiceLocator.GetInstance<IVsPackageInstaller>();

            foreach (EnvDTE.Project project in dte.Solution.Projects)
            {
                _task = System.Threading.Tasks.Task.Run(() => services.InstallPackage("https://api.nuget.org/v2/", project, "newtonsoft.json", "6.0.4", false));
                return;
            }
        }

        private void InstallPackageEmptyVersionTest(object sender, EventArgs e)
        {
            EnvDTE.DTE dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
            IVsPackageInstaller services = ServiceLocator.GetInstance<IVsPackageInstaller>();

            foreach (EnvDTE.Project project in dte.Solution.Projects)
            {
                _task = System.Threading.Tasks.Task.Run(() => services.InstallPackage("https://api.nuget.org/v2/", project, "newtonsoft.json", "", false));
                return;
            }
        }

        private void InstallBadSourceTest(object sender, EventArgs e)
        {
            EnvDTE.DTE dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
            IVsPackageInstaller services = ServiceLocator.GetInstance<IVsPackageInstaller>();

            foreach (EnvDTE.Project project in dte.Solution.Projects)
            {
                _task = System.Threading.Tasks.Task.Run(() => services.InstallPackage("http://packagesource", project, "newtonsoft.json", "6.0.4", false));
                return;
            }
        }

        private void InstallNoSourceTest(object sender, EventArgs e)
        {
            EnvDTE.DTE dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
            IVsPackageInstaller services = ServiceLocator.GetInstance<IVsPackageInstaller>();

            foreach (EnvDTE.Project project in dte.Solution.Projects)
            {
                // install the package from any available source
                _task = System.Threading.Tasks.Task.Run(() => services.InstallPackage(null, project, "newtonsoft.json", "6.0.4", false));
                return;
            }
        }

        private void GetInstalledPackagesTest(object sender, EventArgs e)
        {
            EnvDTE.DTE dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
            IVsPackageInstallerServices services = ServiceLocator.GetInstance<IVsPackageInstallerServices>();

            var allPackages = services.GetInstalledPackages();

            DisplayMessage("Packages in solution", String.Join(", ", allPackages.Select(p => String.Format("[{0} {1}]", p.Id, p.InstallPath))));

            foreach (EnvDTE.Project project in dte.Solution.Projects)
            {
                var projPackages = services.GetInstalledPackages(project);

                DisplayMessage("Project Packages", String.Join(", ", allPackages.Select(p => String.Format("[{0} {1}]", p.Id, p.InstallPath))));
            }
        }


        private void DisplayMessage(string test, string message)
        {
            // Show a Message Box to prove we were here
            IVsUIShell uiShell = (IVsUIShell)GetService(typeof(SVsUIShell));
            Guid clsid = Guid.Empty;
            int result;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(uiShell.ShowMessageBox(
                       0,
                       ref clsid,
                       test,
                       message,
                       string.Empty,
                       0,
                       OLEMSGBUTTON.OLEMSGBUTTON_OK,
                       OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST,
                       OLEMSGICON.OLEMSGICON_INFO,
                       0,        // false
                       out result));
        }

    }
}
