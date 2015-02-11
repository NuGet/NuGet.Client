using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.PackageManagement.PowerShellCmdlets;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using EnvDTEProject = EnvDTE.Project;

namespace NuGetConsole
{
    [Export(typeof(IScriptExecutor))]
    public class VSScriptExecutor : IScriptExecutor
    {
        private readonly Lazy<IHost> _host;
        private readonly ISolutionManager _solutionManager = ServiceLocator.GetInstance<ISolutionManager>();
        private bool _skipPSScriptExecution;

        public VSScriptExecutor()
        {
            _host = new Lazy<IHost>(GetHost);
        }

        private IHost Host
        {
            get
            {
                return _host.Value;
            }
        }

        [Import]
        public IPowerConsoleWindow PowerConsoleWindow
        {
            get;
            set;
        }

        [Import]
        public IOutputConsoleProvider OutputConsoleProvider
        {
            get;
            set;
        }

        public bool Execute(string packageInstallPath, string scriptRelativePath, ZipArchive packageZipArchive, EnvDTEProject envDTEProject,
            NuGetProject nuGetProject, INuGetProjectContext nuGetProjectContext)
        {
            string scriptFullPath = Path.Combine(packageInstallPath, scriptRelativePath);
            return ExecuteCore(scriptFullPath, packageInstallPath, packageZipArchive, envDTEProject, nuGetProject, nuGetProjectContext);
        }

        private bool ExecuteCore(
            string fullScriptPath,
            string packageInstallPath,
            ZipArchive packageZipArchive,
            EnvDTEProject envDTEProject,
            NuGetProject nuGetProject,
            INuGetProjectContext nuGetProjectContext)
        {
            if (File.Exists(fullScriptPath))
            {
                PackageIdentity packageIdentity = null;
                if (envDTEProject != null)
                {
                    NuGetFramework targetFramework;
                    nuGetProject.TryGetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework, out targetFramework);

                    // targetFramework can be null for unknown project types
                    string shortFramework = targetFramework == null ? string.Empty : targetFramework.GetShortFolderName();
                    var packageReader = new PackageReader(packageZipArchive);
                    packageIdentity = packageReader.GetIdentity();

                    nuGetProjectContext.Log(MessageLevel.Debug, NuGet.ProjectManagement.Strings.Debug_TargetFrameworkInfoPrefix, packageIdentity,
                        envDTEProject.Name, shortFramework);

                    //logger.Log(MessageLevel.Debug, NuGetResources.Debug_TargetFrameworkInfo_PowershellScripts,
                    //    Path.GetDirectoryName(scriptFile.Path), VersionUtility.GetTargetFrameworkLogString(scriptFile.TargetFramework));
                }
                string toolsPath = Path.GetDirectoryName(fullScriptPath);
                string command = "$__pc_args=@(); $input|%{$__pc_args+=$_}; & "
                    + PathUtility.EscapePSPath(fullScriptPath)
                    + " $__pc_args[0] $__pc_args[1] $__pc_args[2] $__pc_args[3]; Remove-Variable __pc_args -Scope 0";

                object[] inputs = new object[] { packageInstallPath, toolsPath, packageZipArchive, envDTEProject };

                IPSNuGetProjectContext psNuGetProjectContext = nuGetProjectContext as IPSNuGetProjectContext;

                if (fullScriptPath.EndsWith(PowerShellScripts.Init, StringComparison.OrdinalIgnoreCase))
                {
                    _skipPSScriptExecution = IsPackageInstalledInSolution(packageIdentity);
                }
                else
                {
                    _skipPSScriptExecution = false;
                }

                if (!_skipPSScriptExecution)
                {
                    if (psNuGetProjectContext != null && psNuGetProjectContext.IsExecuting && psNuGetProjectContext.CurrentPSCmdlet != null)
                    {
                        var psVariable = psNuGetProjectContext.CurrentPSCmdlet.SessionState.PSVariable;

                        // set temp variables to pass to the script
                        psVariable.Set("__rootPath", packageInstallPath);
                        psVariable.Set("__toolsPath", toolsPath);
                        psVariable.Set("__package", packageZipArchive);
                        psVariable.Set("__project", envDTEProject);

                        psNuGetProjectContext.ExecutePSScript(fullScriptPath);
                    }
                    else
                    {
                        string logMessage = String.Format(CultureInfo.CurrentCulture, Resources.ExecutingScript, fullScriptPath);

                        // logging to both the Output window and progress window.
                        nuGetProjectContext.Log(MessageLevel.Info, logMessage);
                        IConsole console = OutputConsoleProvider.CreateOutputConsole(requirePowerShellHost: true);
                        Host.Execute(console, command, inputs);
                    }

                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Determine if the package is installed in any of the projects in solution, via packages.config
        /// </summary>
        /// <returns></returns>
        private bool IsPackageInstalledInSolution(PackageIdentity identity)
        {
            if (identity != null)
            {
                IEnumerable<NuGetProject> projects = _solutionManager.GetNuGetProjects();
                foreach (NuGetProject project in projects)
                {
                    IEnumerable<PackageReference> installedRefs = project.GetInstalledPackagesAsync(CancellationToken.None).Result;
                    PackageIdentity match = installedRefs.Select(v => v.PackageIdentity).Where(p => p.Equals(identity)).FirstOrDefault();
                    if (match != null)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private IHost GetHost()
        {
            // create the console and instantiate the PS host on demand
            IConsole console = OutputConsoleProvider.CreateOutputConsole(requirePowerShellHost: true);
            IHost host = console.Host;

            // start the console 
            console.Dispatcher.Start();

            // gives the host a chance to do initialization works before dispatching commands to it
            host.Initialize(console);

            // after the host initializes, it may set IsCommandEnabled = false
            if (host.IsCommandEnabled)
            {
                return host;
            }
            else
            {
                // the PowerShell host fails to initialize if group policy restricts to AllSigned
                throw new InvalidOperationException(Resources.Console_InitializeHostFails);
            }
        }
    }
}
