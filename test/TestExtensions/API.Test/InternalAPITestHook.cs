using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio;

namespace API.Test
{
    public static class InternalAPITestHook
    {
        public static void InstallPackageApi(string id, string version)
        {
            var dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
            IVsPackageInstaller services = ServiceLocator.GetInstance<IVsPackageInstaller>();

            foreach (EnvDTE.Project project in dte.Solution.Projects)
            {
                services.InstallPackage(null, project, id, version, false);
                return;
            }
        }

        public static void InstallPackageApiBadSource(string id, string version)
        {
            EnvDTE.DTE dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
            IVsPackageInstaller services = ServiceLocator.GetInstance<IVsPackageInstaller>();

            foreach (EnvDTE.Project project in dte.Solution.Projects)
            {
                services.InstallPackage("http://packagesource", project, id, version, false);
                return;
            }
        }

        public static void UninstallPackageApi(string id, bool dependency)
        {
            EnvDTE.DTE dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
            IVsPackageUninstaller uninstaller = ServiceLocator.GetInstance<IVsPackageUninstaller>();

            foreach (EnvDTE.Project project in dte.Solution.Projects)
            {
                uninstaller.UninstallPackage(project, id, dependency);
                return;
            }
        }

        public static void RestorePackageApi()
        {
            EnvDTE.DTE dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
            IVsPackageRestorer restorer = ServiceLocator.GetInstance<IVsPackageRestorer>();

            foreach (EnvDTE.Project project in dte.Solution.Projects)
            {
                restorer.RestorePackages(project);
                return;
            }
        }
    }
}
