// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using System.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGetConsole;
using Task = System.Threading.Tasks.Task;

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Restore packages menu command handler.
    /// </summary>
    internal sealed class SolutionRestoreCommand
    {
        private static SolutionRestoreCommand _instance;

        private const int CommandId = PkgCmdIDList.cmdidRestorePackages;
        private static readonly Guid CommandSet = GuidList.guidNuGetDialogCmdSet;

        private Lazy<INuGetUILogger> _logger;
        private Lazy<ISolutionRestoreWorker> _solutionRestoreWorker;
        private Lazy<ISolutionManager> _solutionManager;
        private Lazy<IConsoleStatus> _consoleStatus;

        private INuGetUILogger Logger => _logger.Value;
        private ISolutionRestoreWorker SolutionRestoreWorker => _solutionRestoreWorker.Value;
        private ISolutionManager SolutionManager => _solutionManager.Value;
        private IConsoleStatus ConsoleStatus => _consoleStatus.Value;

        private readonly IVsMonitorSelection _vsMonitorSelection;
        private uint _solutionNotBuildingAndNotDebuggingContextCookie;

        private SolutionRestoreCommand(
            IMenuCommandService commandService,
            IVsMonitorSelection vsMonitorSelection,
            IComponentModel componentModel)
        {
            if (componentModel == null)
            {
                throw new ArgumentNullException(nameof(componentModel));
            }

            var menuCommandId = new CommandID(CommandSet, CommandId);
            var menuItem = new OleMenuCommand(
                OnRestorePackages, null, BeforeQueryStatusForPackageRestore, menuCommandId);
            commandService?.AddCommand(menuItem);

            _vsMonitorSelection = vsMonitorSelection;

            // get the solution not building and not debugging cookie
            var guid = VSConstants.UICONTEXT.SolutionExistsAndNotBuildingAndNotDebugging_guid;
            _vsMonitorSelection.GetCmdUIContextCookie(ref guid, out _solutionNotBuildingAndNotDebuggingContextCookie);

            _logger = new Lazy<INuGetUILogger>(
                () => componentModel.GetService<INuGetUILogger>());

            _solutionRestoreWorker = new Lazy<ISolutionRestoreWorker>(
                () => componentModel.GetService<ISolutionRestoreWorker>());

            _solutionManager = new Lazy<ISolutionManager>(
                () => componentModel.GetService<ISolutionManager>());

            _consoleStatus = new Lazy<IConsoleStatus>(
                () => componentModel.GetService<IConsoleStatus>());
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as IMenuCommandService;
            var vsMonitorSelection = await package.GetServiceAsync(typeof(IVsMonitorSelection)) as IVsMonitorSelection;
            var componentModel = await package.GetComponentModelAsync();

            _instance = new SolutionRestoreCommand(commandService, vsMonitorSelection, componentModel);
            componentModel.DefaultCompositionService.SatisfyImportsOnce(_instance);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void OnRestorePackages(object sender, EventArgs args)
        {
            if (!SolutionRestoreWorker.IsBusy)
            {
                SolutionRestoreWorker.Restore(SolutionRestoreRequest.ByMenu());
            }
            else
            {
                // QueryStatus should disable the context menu in most of the cases.
                // Except when NuGetPackage was not loaded before VS won't send QueryStatus.
                Logger.Log(MessageLevel.Info, Resources.SolutionRestoreFailed_RestoreWorkerIsBusy);
            }
        }

        private void BeforeQueryStatusForPackageRestore(object sender, EventArgs args)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run((Func<Task>)async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                OleMenuCommand command = (OleMenuCommand)sender;

                // Enable the 'Restore NuGet Packages' dialog menu
                // - if the console is NOT busy executing a command, AND
                // - if the restore worker is not executing restore operation, AND
                // - if the solution exists and not debugging and not building AND
                // - if the solution is DPL enabled or there are NuGetProjects. This means that there loaded, supported projects
                // Checking for DPL more is a temporary code until we've the capability to get nuget projects
                // even in DPL mode. See https://github.com/NuGet/Home/issues/3711
                command.Enabled = !ConsoleStatus.IsBusy &&
                    !SolutionRestoreWorker.IsBusy &&
                    IsSolutionExistsAndNotDebuggingAndNotBuilding() &&
                    (SolutionManager.IsSolutionDPLEnabled || Enumerable.Any<NuGetProject>(SolutionManager.GetNuGetProjects()));
            });
        }

        private bool IsSolutionExistsAndNotDebuggingAndNotBuilding()
        {
            int pfActive;
            var result = _vsMonitorSelection.IsCmdUIContextActive(_solutionNotBuildingAndNotDebuggingContextCookie, out pfActive);
            return (result == VSConstants.S_OK && pfActive > 0);
        }
    }
}
