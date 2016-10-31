// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using NuGet.PackageManagement;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGetConsole;
using Task = System.Threading.Tasks.Task;

namespace NuGetVSExtension
{
    /// <summary>
    /// Restore packages menu command handler.
    /// </summary>
    internal sealed class RestorePackagesCommand
    {
        private static RestorePackagesCommand _instance;

        private const int CommandId = PkgCmdIDList.cmdidRestorePackages;
        private static readonly Guid CommandSet = GuidList.guidNuGetDialogCmdSet;

        private readonly NuGetPackage _package;
        private readonly INuGetUILogger _logger;
        private readonly Lazy<ISolutionRestoreWorker> _restoreWorker;
        private readonly Lazy<ISolutionManager> _solutionManager;
        private readonly Lazy<IConsoleStatus> _consoleStatus;

        private ISolutionRestoreWorker SolutionRestoreWorker => _restoreWorker.Value;
        private ISolutionManager SolutionManager => _solutionManager.Value;
        private IConsoleStatus ConsoleStatus => _consoleStatus.Value;

        private RestorePackagesCommand(
            NuGetPackage package,
            IComponentModel componentModel,
            IMenuCommandService commandService,
            INuGetUILogger logger)
        {
            _package = package;
            _logger = logger;

            _restoreWorker = new Lazy<ISolutionRestoreWorker>(
                () => componentModel.GetService<ISolutionRestoreWorker>());

            _solutionManager = new Lazy<ISolutionManager>(
                () => componentModel.GetService<ISolutionManager>());

            _consoleStatus = new Lazy<IConsoleStatus>(
                () => componentModel.GetService<IConsoleStatus>());

            var menuCommandId = new CommandID(CommandSet, CommandId);
            var menuItem = new OleMenuCommand(
                OnRestorePackages, null, BeforeQueryStatusForPackageRestore, menuCommandId);
            commandService?.AddCommand(menuItem);
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(NuGetPackage package, INuGetUILogger logger)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            var componentModel = await package.GetServiceAsync(typeof(SComponentModel)) as IComponentModel;
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;

            _instance = new RestorePackagesCommand(package, componentModel, commandService, logger);
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
                _logger.Log(MessageLevel.Info, Resources.SolutionRestoreFailed_RestoreWorkerIsBusy);
            }
        }

        private void BeforeQueryStatusForPackageRestore(object sender, EventArgs args)
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
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
                command.Enabled = !ConsoleStatus.IsBusy &&
                    !SolutionRestoreWorker.IsBusy &&
                    _package.IsSolutionExistsAndNotDebuggingAndNotBuilding() &&
                    (SolutionManager.IsSolutionDPLEnabled || SolutionManager.GetNuGetProjects().Any());
            });
        }
    }
}
