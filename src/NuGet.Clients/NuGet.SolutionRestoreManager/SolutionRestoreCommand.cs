// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using System.Linq;
using Microsoft.VisualStudio;
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

        [Import]
        private Lazy<INuGetUILogger> Logger { get; set; }

        [Import]
        private Lazy<ISolutionRestoreWorker> SolutionRestoreWorker { get; set; }

        [Import]
        private Lazy<ISolutionManager> SolutionManager { get; set; }

        [Import]
        private Lazy<IConsoleStatus> ConsoleStatus { get; set; }

        private readonly IVsMonitorSelection _vsMonitorSelection;
        private uint _solutionNotBuildingAndNotDebuggingContextCookie;

        private SolutionRestoreCommand(
            IMenuCommandService commandService,
            IVsMonitorSelection vsMonitorSelection)
        {
            var menuCommandId = new CommandID(CommandSet, CommandId);
            var menuItem = new OleMenuCommand(
                OnRestorePackages, null, BeforeQueryStatusForPackageRestore, menuCommandId);
            commandService?.AddCommand(menuItem);

            _vsMonitorSelection = vsMonitorSelection;

            // get the solution not building and not debugging cookie
            var guid = VSConstants.UICONTEXT.SolutionExistsAndNotBuildingAndNotDebugging_guid;
            _vsMonitorSelection.GetCmdUIContextCookie(ref guid, out _solutionNotBuildingAndNotDebuggingContextCookie);
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

            var commandService = await package.GetServiceAsync<IMenuCommandService>();
            var vsMonitorSelection = await package.GetServiceAsync<IVsMonitorSelection>();

            _instance = new SolutionRestoreCommand(commandService, vsMonitorSelection);

            var componentModel = await package.GetComponentModelAsync();
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
            if (!SolutionRestoreWorker.Value.IsBusy)
            {
                SolutionRestoreWorker.Value.Restore(SolutionRestoreRequest.ByMenu());
            }
            else
            {
                // QueryStatus should disable the context menu in most of the cases.
                // Except when NuGetPackage was not loaded before VS won't send QueryStatus.
                Logger.Value.Log(MessageLevel.Info, Resources.SolutionRestoreFailed_RestoreWorkerIsBusy);
            }
        }

        private void BeforeQueryStatusForPackageRestore(object sender, EventArgs args)
        {
            ThreadHelper.JoinableTaskFactory.Run((Func<Task>)async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                OleMenuCommand command = (OleMenuCommand)sender;

                // Enable the 'Restore NuGet Packages' dialog menu
                // - if the console is NOT busy executing a command, AND
                // - if the restore worker is not executing restore operation, AND
                // - if the solution exists and not debugging and not building AND
                // - if the solution is DPL enabled or there are NuGetProjects. This means that there loaded, supported projects
                // Checking for DPL more is a temporary code until we've the capability to get nuget projects
                // even in DPL mode. See https://github.com/NuGet/Home/issues/3711
                command.Enabled = !ConsoleStatus.Value.IsBusy &&
                    !SolutionRestoreWorker.Value.IsBusy &&
                    IsSolutionExistsAndNotDebuggingAndNotBuilding() &&
                    (SolutionManager.Value.IsSolutionDPLEnabled || Enumerable.Any<NuGetProject>(SolutionManager.Value.GetNuGetProjects()));
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
