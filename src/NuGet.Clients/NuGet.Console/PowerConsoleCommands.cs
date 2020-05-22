using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using NuGetConsole.Implementation;
using System.ComponentModel.Design;
using NuGetConsole.Implementation.PowerConsole;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.ComponentModelHost;
using NuGet.VisualStudio;
using System.Runtime.InteropServices;

namespace NuGetConsole
{
    public class PowerConsoleCommands
    {
        public static PowerConsoleCommands Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Get VS IComponentModel service.
        /// </summary>
        private IComponentModel ComponentModel
        {
            get; 
        }

        private PowerConsoleWindow PowerConsoleWindow
        {
            get;
        }

        private HostInfo ActiveHostInfo => PowerConsoleWindow.ActiveHostInfo;

        private IWpfConsole WpfConsole
        {
            get;
        }

        public static async System.Threading.Tasks.Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var cmdSvc = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            var comSvc = await package.GetComponentModelAsync();

            Instance = new PowerConsoleCommands(cmdSvc, comSvc);
        }

        private PowerConsoleCommands(OleMenuCommandService commandService, IComponentModel comModelSvc)
        {
            if (commandService == null)
            {
                throw new ArgumentNullException(nameof(commandService));
            }

            // init services
            ComponentModel = comModelSvc;
            PowerConsoleWindow = ComponentModel.GetService<IPowerConsoleWindow>() as PowerConsoleWindow;
            WpfConsole = ActiveHostInfo.WpfConsole;

            InitCommands(commandService);
        }

        public void InitCommands(OleMenuCommandService mcs)
        {
            // Get list command for the Feed combo
            var sourcesListCommandID = new CommandID(GuidList.guidNuGetCmdSet, PkgCmdIDList.cmdidSourcesList);
            mcs.AddCommand(new OleMenuCommand(SourcesList_Exec, sourcesListCommandID));

            // invoke command for the Feed combo
            var sourcesCommandID = new CommandID(GuidList.guidNuGetCmdSet, PkgCmdIDList.cmdidSources);
            mcs.AddCommand(new OleMenuCommand(Sources_Exec, sourcesCommandID));

            // get default project command
            var projectsListCommandID = new CommandID(GuidList.guidNuGetCmdSet, PkgCmdIDList.cmdidProjectsList);
            mcs.AddCommand(new OleMenuCommand(ProjectsList_Exec, projectsListCommandID));

            // invoke command for the Default project combo
            var projectsCommandID = new CommandID(GuidList.guidNuGetCmdSet, PkgCmdIDList.cmdidProjects);
            mcs.AddCommand(new OleMenuCommand(Projects_Exec, projectsCommandID));

            // clear console command
            var clearHostCommandID = new CommandID(GuidList.guidNuGetCmdSet, PkgCmdIDList.cmdidClearHost);
            mcs.AddCommand(new OleMenuCommand(ClearHost_Exec, clearHostCommandID));

            // terminate command execution command
            var stopHostCommandID = new CommandID(GuidList.guidNuGetCmdSet, PkgCmdIDList.cmdidStopHost);
            mcs.AddCommand(new OleMenuCommand(StopHost_Exec, stopHostCommandID));
        }

        private void SourcesList_Exec(object sender, EventArgs e)
        {
            if (e is OleMenuCmdEventArgs args)
            {
                if (args.InValue != null || args.OutValue == IntPtr.Zero)
                {
                    throw new ArgumentException("Invalid argument", nameof(e));
                }
                Marshal.GetNativeVariantForObject(PowerConsoleWindow.PackageSources, args.OutValue);
            }
        }

        /// <summary>
        /// Called to retrieve current combo item name or to select a new item.
        /// </summary>
        private void Sources_Exec(object sender, EventArgs e)
        {
            if (e is OleMenuCmdEventArgs args)
            {
                if (args.InValue != null && args.InValue is int index) // Selected a feed
                {
                    if (index >= 0 && index < PowerConsoleWindow.PackageSources.Length)
                    {
                        PowerConsoleWindow.ActivePackageSource = PowerConsoleWindow.PackageSources[index];
                    }
                }
                else if (args.OutValue != IntPtr.Zero) // Query selected feed name
                {
                    var displayName = PowerConsoleWindow.ActivePackageSource ?? string.Empty;
                    Marshal.GetNativeVariantForObject(displayName, args.OutValue);
                }
            }
        }

        private void ProjectsList_Exec(object sender, EventArgs e)
        {
            if (e is OleMenuCmdEventArgs args)
            {
                if (args.InValue != null || args.OutValue == IntPtr.Zero)
                {
                    throw new ArgumentException("Invalid argument", nameof(e));
                }

                // get project list here
                Marshal.GetNativeVariantForObject(PowerConsoleWindow.AvailableProjects, args.OutValue);
            }
        }

        /// <summary>
        /// Called to retrieve current combo item name or to select a new item.
        /// </summary>
        private void Projects_Exec(object sender, EventArgs e)
        {
            if (e is OleMenuCmdEventArgs args)
            {
                var pcw = PowerConsoleWindow;
                if (args.InValue != null && args.InValue is int index)
                {
                    // Selected a default projects
                    if (index >= 0 && index < pcw.AvailableProjects.Length)
                    {
                        pcw.SetDefaultProjectIndex(index);
                    }
                }
                else if (args.OutValue != IntPtr.Zero)
                {
                    var displayName = pcw.DefaultProject ?? string.Empty;
                    Marshal.GetNativeVariantForObject(displayName, args.OutValue);
                }
            }
        }

        /// <summary>
        /// ClearHost command handler.
        /// </summary>
        private void ClearHost_Exec(object sender, EventArgs e)
        {
            if (WpfConsole != null)
            {
                WpfConsole.Dispatcher.ClearConsole();
            }
        }

        private void StopHost_Exec(object sender, EventArgs e)
        {
            if (WpfConsole != null)
            {
                WpfConsole.Host.Abort();
            }
        }
    }
}
