// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.PackageManagement.PowerShellCmdlets;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using EnvDTEProject = EnvDTE.Project;
using Strings = NuGet.ProjectManagement.Strings;
using Task = System.Threading.Tasks.Task;

namespace NuGetConsole
{
    [Export(typeof(IScriptExecutor))]
    public class ScriptExecutor : IScriptExecutor
    {
        private AsyncLazy<IHost> Host { get; }
        private ISolutionManager SolutionManager { get; }  = ServiceLocator.GetInstance<ISolutionManager>();
        private string SolutionDirectory { get; set; }
        private ISettings Settings { get; } = ServiceLocator.GetInstance<ISettings>();
        private ConcurrentDictionary<PackageIdentity, PackageInitPS1State> InitScriptExecutions
            = new ConcurrentDictionary<PackageIdentity, PackageInitPS1State>(PackageIdentityComparer.Default);

        public ScriptExecutor()
        {
            Host = new AsyncLazy<IHost>(GetHostAsync, ThreadHelper.JoinableTaskFactory);
            Reset();
        }

        [Import]
        public IPowerConsoleWindow PowerConsoleWindow { get; set; }

        [Import]
        public IOutputConsoleProvider OutputConsoleProvider { get; set; }

        public void Reset()
        {
            if (SolutionManager.IsSolutionAvailable)
            {
                SolutionDirectory = SolutionManager.SolutionDirectory;
            }
            else
            {
                SolutionDirectory = null;
            }

            InitScriptExecutions.Clear();
        }

        public async Task<bool> ExecuteAsync(
            PackageIdentity packageIdentity,
            string packageInstallPath,
            string scriptRelativePath,
            EnvDTEProject envDTEProject,
            NuGetProject nuGetProject,
            INuGetProjectContext nuGetProjectContext,
            bool throwOnFailure)
        {
            string scriptFullPath = Path.Combine(packageInstallPath, scriptRelativePath);
            return await ExecuteCoreAsync(
                packageIdentity,
                scriptFullPath,
                packageInstallPath,
                envDTEProject,
                nuGetProject,
                nuGetProjectContext,
                throwOnFailure);
        }

        public bool TryMarkVisited(PackageIdentity packageIdentity, PackageInitPS1State initPS1State)
        {
            return InitScriptExecutions.TryAdd(packageIdentity, initPS1State);
        }

        public async Task<bool> ExecuteInitScriptAsync(PackageIdentity packageIdentity)
        {
            var result = false;
            // Reserve the key. We can remove if the package has not been restored.
            if (TryMarkVisited(packageIdentity, PackageInitPS1State.NotFound))
            {
                var packageInstalledPath = GetPackageInstalledPath(packageIdentity);
                if (!string.IsNullOrEmpty(packageInstalledPath))
                {
                    var initPS1Path = Path.Combine(packageInstalledPath, "tools", PowerShellScripts.Init);
                    if (File.Exists(initPS1Path))
                    {
                        // Init.ps1 is present and will be executed.
                        InitScriptExecutions.TryUpdate(
                            packageIdentity,
                            PackageInitPS1State.FoundAndExecuted,
                            PackageInitPS1State.NotFound);

                        var scriptPackage = new ScriptPackage(
                            packageIdentity.Id,
                            packageIdentity.Version.ToString(),
                            packageInstalledPath);
                        var toolsPath = Path.GetDirectoryName(initPS1Path);

                        await ExecuteScriptCoreAsync(
                            scriptPackage,
                            packageInstalledPath,
                            initPS1Path,
                            toolsPath,
                            envDTEProject: null);

                        result = true;
                    }
                }
                else
                {
                    // Package is not restored. Do not cache the results.
                    PackageInitPS1State dummy;
                    InitScriptExecutions.TryRemove(packageIdentity, out dummy);
                    result = false;
                }
            }
            else
            {
                // Key is already present. Simply access its value
                result = (InitScriptExecutions[packageIdentity] == PackageInitPS1State.FoundAndExecuted);
            }

            return result;
        }

        private async Task<bool> ExecuteCoreAsync(
            PackageIdentity packageIdentity,
            string fullScriptPath,
            string packageInstallPath,
            EnvDTEProject envDTEProject,
            NuGetProject nuGetProject,
            INuGetProjectContext nuGetProjectContext,
            bool throwOnFailure)
        {
            if (File.Exists(fullScriptPath))
            {
                if (fullScriptPath.EndsWith(PowerShellScripts.Init, StringComparison.OrdinalIgnoreCase)
                    && !TryMarkVisited(packageIdentity, PackageInitPS1State.FoundAndExecuted))
                {
                    return true;
                }

                ScriptPackage package = null;
                if (envDTEProject != null)
                {
                    NuGetFramework targetFramework;
                    nuGetProject.TryGetMetadata(NuGetProjectMetadataKeys.TargetFramework, out targetFramework);

                    // targetFramework can be null for unknown project types
                    string shortFramework = targetFramework?.GetShortFolderName() ?? string.Empty;

                    nuGetProjectContext.Log(MessageLevel.Debug, Strings.Debug_TargetFrameworkInfoPrefix, packageIdentity,
                        envDTEProject.Name, shortFramework);
                }

                if (packageIdentity != null)
                {
                    package = new ScriptPackage(packageIdentity.Id, packageIdentity.Version.ToString(), packageInstallPath);
                }

                string toolsPath = Path.GetDirectoryName(fullScriptPath);
                IPSNuGetProjectContext psNuGetProjectContext = nuGetProjectContext as IPSNuGetProjectContext;
                if (psNuGetProjectContext != null
                    && psNuGetProjectContext.IsExecuting
                    && psNuGetProjectContext.CurrentPSCmdlet != null)
                {
                    var psVariable = psNuGetProjectContext.CurrentPSCmdlet.SessionState.PSVariable;

                    // set temp variables to pass to the script
                    psVariable.Set("__rootPath", packageInstallPath);
                    psVariable.Set("__toolsPath", toolsPath);
                    psVariable.Set("__package", package);
                    psVariable.Set("__project", envDTEProject);

                    psNuGetProjectContext.ExecutePSScript(fullScriptPath, throwOnFailure);
                }
                else
                {
                    string logMessage = String.Format(CultureInfo.CurrentCulture, Resources.ExecutingScript, fullScriptPath);
                    // logging to both the Output window and progress window.
                    nuGetProjectContext.Log(MessageLevel.Info, logMessage);
                    try
                    {
                        await ExecuteScriptCoreAsync(
                            package,
                            packageInstallPath,
                            fullScriptPath,
                            toolsPath,
                            envDTEProject);
                    }
                    catch (Exception ex)
                    {
                        // throwFailure is set by Package Manager.
                        if (throwOnFailure)
                        {
                            throw;
                        }
                        nuGetProjectContext.Log(MessageLevel.Warning, ex.Message);
                    }
                }

                return true;
            }
            else
            {
                if (fullScriptPath.EndsWith(PowerShellScripts.Init, StringComparison.OrdinalIgnoreCase))
                {
                    TryMarkVisited(packageIdentity, PackageInitPS1State.NotFound);
                }
            }
            return false;
        }

        private string GetPackageInstalledPath(PackageIdentity packageIdentity)
        {
            string effectiveGlobalPackagesFolder = null;
            if (!string.IsNullOrEmpty(SolutionDirectory))
            {
                // Package not found in packages folder
                effectiveGlobalPackagesFolder = BuildIntegratedProjectUtility.GetEffectiveGlobalPackagesFolder(
                                                        SolutionDirectory,
                                                        Settings);
            }
            else
            {
                // No solution available. Use default global packages folder
                effectiveGlobalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(Settings);
            }

            var versionFolderPathResolver = new VersionFolderPathResolver(effectiveGlobalPackagesFolder);
            var hashPath = versionFolderPathResolver.GetHashPath(packageIdentity.Id, packageIdentity.Version);

            if (File.Exists(hashPath))
            {
                var packageInstallPath = Path.GetDirectoryName(hashPath);
                return packageInstallPath;
            }

            return null;
        }

        private async Task ExecuteScriptCoreAsync(
            ScriptPackage package,
            string packageInstallPath,
            string fullScriptPath,
            string toolsPath,
            EnvDTEProject envDTEProject)
        {
            string command = "$__pc_args=@(); $input|%{$__pc_args+=$_}; & "
                             + PathUtility.EscapePSPath(fullScriptPath)
                             + " $__pc_args[0] $__pc_args[1] $__pc_args[2] $__pc_args[3]; "
                             + "Remove-Variable __pc_args -Scope 0";

            object[] inputs = { packageInstallPath, toolsPath, package, envDTEProject };
            IConsole console = OutputConsoleProvider.CreateOutputConsole(requirePowerShellHost: true);
            var host = await Host.GetValueAsync();

            // Host.Execute calls powershell's pipeline.Invoke and blocks the calling thread
            // to switch to powershell pipeline execution thread. In order not to block the UI thread,
            // go off the UI thread. This is important, since, switches to UI thread,
            // using SwitchToMainThreadAsync will deadlock otherwise
            await Task.Run(() => host.Execute(console, command, inputs));
        }

        private async Task<IHost> GetHostAsync()
        {
            // Since we are creating the output console and the output window pane, switch to the main thread
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // create the console and instantiate the PS host on demand
            IConsole console = OutputConsoleProvider.CreateOutputConsole(requirePowerShellHost: true);
            IHost host = console.Host;

            // start the console 
            console.Dispatcher.Start();

            // gives the host a chance to do initialization works before dispatching commands to it
            // Host.Initialize calls powershell's pipeline.Invoke and blocks the calling thread
            // to switch to powershell pipeline execution thread. In order not to block the UI thread, go off the UI thread.
            // This is important, since, switches to UI thread, using SwitchToMainThreadAsync will deadlock otherwise
            await Task.Run(() => host.Initialize(console));

            // after the host initializes, it may set IsCommandEnabled = false
            if (host.IsCommandEnabled)
            {
                return host;
            }
            // the PowerShell host fails to initialize if group policy restricts to AllSigned
            throw new InvalidOperationException(Resources.Console_InitializeHostFails);
        }
    }
}
