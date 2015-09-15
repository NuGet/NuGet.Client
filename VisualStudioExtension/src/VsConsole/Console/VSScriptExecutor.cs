// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.PackageManagement.PowerShellCmdlets;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using EnvDTEProject = EnvDTE.Project;
using Strings = NuGet.ProjectManagement.Strings;
using Task = System.Threading.Tasks.Task;

namespace NuGetConsole
{
    [Export(typeof(IScriptExecutor))]
    public class VSScriptExecutor : IScriptExecutor
    {
        private AsyncLazy<IHost> Host { get; }
        private readonly ISolutionManager _solutionManager = ServiceLocator.GetInstance<ISolutionManager>();
        private bool _skipPSScriptExecution;

        public VSScriptExecutor()
        {
            Host = new AsyncLazy<IHost>(GetHostAsync, ThreadHelper.JoinableTaskFactory);
        }

        [Import]
        public IPowerConsoleWindow PowerConsoleWindow { get; set; }

        [Import]
        public IOutputConsoleProvider OutputConsoleProvider { get; set; }

        public async Task<bool> ExecuteAsync(PackageIdentity packageIdentity, string packageInstallPath, string scriptRelativePath, EnvDTEProject envDTEProject,
            NuGetProject nuGetProject, INuGetProjectContext nuGetProjectContext, bool throwOnFailure)
        {
            string scriptFullPath = Path.Combine(packageInstallPath, scriptRelativePath);
            return await ExecuteCoreAsync(packageIdentity, scriptFullPath, packageInstallPath, envDTEProject, nuGetProject, nuGetProjectContext, throwOnFailure);
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
                ScriptPackage package = null;
                if (envDTEProject != null)
                {
                    NuGetFramework targetFramework;
                    nuGetProject.TryGetMetadata(NuGetProjectMetadataKeys.TargetFramework, out targetFramework);

                    // targetFramework can be null for unknown project types
                    string shortFramework = targetFramework == null ? string.Empty : targetFramework.GetShortFolderName();

                    nuGetProjectContext.Log(MessageLevel.Debug, Strings.Debug_TargetFrameworkInfoPrefix, packageIdentity,
                        envDTEProject.Name, shortFramework);
                }
                if (packageIdentity != null)
                {
                    package = new ScriptPackage(packageIdentity.Id, packageIdentity.Version.ToString(), packageInstallPath);
                }
                if (fullScriptPath.EndsWith(PowerShellScripts.Init, StringComparison.OrdinalIgnoreCase))
                {
                    _skipPSScriptExecution = await NuGetPackageManager.PackageExistsInAnotherNuGetProject(nuGetProject, packageIdentity,
                        _solutionManager, CancellationToken.None);
                }
                else
                {
                    _skipPSScriptExecution = false;
                }

                if (!_skipPSScriptExecution)
                {
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
                        string command = "$__pc_args=@(); $input|%{$__pc_args+=$_}; & "
                                         + PathUtility.EscapePSPath(fullScriptPath)
                                         + " $__pc_args[0] $__pc_args[1] $__pc_args[2] $__pc_args[3]; Remove-Variable __pc_args -Scope 0";

                        object[] inputs = { packageInstallPath, toolsPath, package, envDTEProject };
                        string logMessage = String.Format(CultureInfo.CurrentCulture, Resources.ExecutingScript, fullScriptPath);

                        // logging to both the Output window and progress window.
                        nuGetProjectContext.Log(MessageLevel.Info, logMessage);
                        IConsole console = OutputConsoleProvider.CreateOutputConsole(requirePowerShellHost: true);
                        try
                        {
                            var host = await Host.GetValueAsync();
                            // Host.Execute calls powershell's pipeline.Invoke and blocks the calling thread
                            // to switch to powershell pipeline execution thread. In order not to block the UI thread, go off the UI thread.
                            // This is important, since, switches to UI thread, using SwitchToMainThreadAsync will deadlock otherwise
                            await Task.Run(() => host.Execute(console, command, inputs));
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
            }
            return false;
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
