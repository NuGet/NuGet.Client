// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using NuGet.PackageManagement;
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

        public static IVsPathContext GetVsPathContext(string projectUniqueName)
        {
            var dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
            var factory = ServiceLocator.GetInstance<IVsPathContextProvider>();

            foreach (EnvDTE.Project project in dte.Solution.Projects)
            {
                if (project.UniqueName == projectUniqueName)
                {
                    return factory.CreateAsync(project, CancellationToken.None).Result;
                }
            }

            return null;
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

        public static bool BatchEventsApi(string id, string version)
        {
            var dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
            var packageProjectEventService = ServiceLocator.GetInstance<IVsPackageInstallerProjectEvents>();
            var installerServices = ServiceLocator.GetInstance<IVsPackageInstaller>();
            var batchStartIds = new List<string>();
            var batchEndIds = new List<string>();

            packageProjectEventService.BatchStart += (args) =>
            {
                batchStartIds.Add(args.BatchId);
            };

            packageProjectEventService.BatchEnd += (args) =>
            {
                batchEndIds.Add(args.BatchId);
            };

            foreach (EnvDTE.Project project in dte.Solution.Projects)
            {
                installerServices.InstallPackage(null, project, id, version, false);
            }

            return batchStartIds.Count == 1 &&
                   batchEndIds.Count == 1 &&
                   batchStartIds[0].Equals(batchEndIds[0], StringComparison.Ordinal);
        }

        public static int ProjectCacheEventApi(string id, string version)
        {
            var dte = ServiceLocator.GetInstance<EnvDTE.DTE>();
            var vsSolutionManager = ServiceLocator.GetInstance<ISolutionManager>();
            var installerServices = ServiceLocator.GetInstance<IVsPackageInstaller>();
            var eventCount = 0;
            Action<object, NuGetEventArgs<string>> eventHandler = delegate (object sender, NuGetEventArgs<string> e)
            {
                eventCount++;
            };

            vsSolutionManager.AfterNuGetCacheUpdated += eventHandler.Invoke;

            foreach (EnvDTE.Project project in dte.Solution.Projects)
            {
                installerServices.InstallPackage(null, project, id, version, false);
            }

            vsSolutionManager.AfterNuGetCacheUpdated -= eventHandler.Invoke;

            return eventCount;
        }
    }
}
