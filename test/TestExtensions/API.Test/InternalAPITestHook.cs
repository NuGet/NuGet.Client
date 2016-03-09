using System;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio;
using Task = System.Threading.Tasks.Task;

namespace API.Test
{
    public static class InternalAPITestHook
    {
        public static void InstallLatestPackageApi(string id, bool prerelease)
        {
            var dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
            var services = ServiceLocator.GetInstance<IVsPackageInstaller2>();

            foreach (EnvDTE.Project project in dte.Solution.Projects)
            {
                services.InstallLatestPackage(null, project, id, prerelease, false);
                return;
            }
        }

        public static void InstallPackageApi(string id, string version)
        {
            InstallPackageApi(null, id, version, false);
        }

        public static void InstallPackageApi(string source, string id, string version, bool prerelease)
        {
            var dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
            IVsPackageInstaller services = ServiceLocator.GetInstance<IVsPackageInstaller>();

            foreach (EnvDTE.Project project in dte.Solution.Projects)
            {
                services.InstallPackage(source, project, id, version, prerelease);
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

        public static bool ExecuteInitScript(string id, string version)
        {
            var scriptExecutor = ServiceLocator.GetInstance<IVsGlobalPackagesInitScriptExecutor>();
            // It is important that this method does not wait on ExecuteInitScriptAsync on the calling thread.
            // Calling thread is powershell execution thread and ExecuteInitScriptAsync needs to switch to
            // Powershell execution thread to execute the scripts
            var task = Task.Run(async () => await scriptExecutor.ExecuteInitScriptAsync(id, version));
            Task.WaitAny(task, Task.Delay(30000));
            if (task.IsCompleted)
            {
                return task.Result;
            }

            return false;
        }
    }
}
