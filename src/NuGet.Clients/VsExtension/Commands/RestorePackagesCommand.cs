// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGetConsole;

namespace NuGetVSExtension
{
    /// <summary>
    /// Restore packages menu command handler.
    /// </summary>
    internal sealed class RestorePackagesCommand
    {
        private const int CommandId = PkgCmdIDList.cmdidRestorePackages;
        private static readonly Guid CommandSet = GuidList.guidNuGetDialogCmdSet;

        private readonly NuGetPackage _package;
        private readonly ISolutionRestoreWorker _restoreWorker;
        private readonly ISolutionManager _solutionManager;
        private readonly IConsoleStatus _consoleStatus;

        /// <summary>
        /// Initializes a new instance of the <see cref="RestorePackagesCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private RestorePackagesCommand(NuGetPackage package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            _package = package;

            var componentModel = package.GetService<SComponentModel, IComponentModel>();

            _restoreWorker = componentModel?.GetService<ISolutionRestoreWorker>();
            Debug.Assert(_restoreWorker != null);

            _solutionManager = componentModel?.GetService<ISolutionManager>();
            Debug.Assert(_solutionManager != null);

            _consoleStatus = componentModel?.GetService<IConsoleStatus>();
            Debug.Assert(_consoleStatus != null);

            var menuCommandId = new CommandID(CommandSet, CommandId);
            var menuItem = new OleMenuCommand(
                OnRestorePackages, null, BeforeQueryStatusForPackageRestore, menuCommandId);
            var commandService = package.GetService<IMenuCommandService, OleMenuCommandService>();
            commandService?.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static RestorePackagesCommand Instance { get; private set; }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(NuGetPackage package)
        {
            Instance = new RestorePackagesCommand(package);
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
            _restoreWorker.Restore(SolutionRestoreRequest.ByMenu());
        }

        private void BeforeQueryStatusForPackageRestore(object sender, EventArgs args)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                OleMenuCommand command = (OleMenuCommand)sender;

                // Enable the 'Restore NuGet Packages' dialog menu
                // a) if the console is NOT busy executing a command, AND
                // b) if the solution exists and not debugging and not building AND
                // c) if there are no NuGetProjects. This means that there no loaded, supported projects
                command.Enabled = !_consoleStatus.IsBusy && 
                    _package.IsSolutionExistsAndNotDebuggingAndNotBuilding() && 
                    _solutionManager.GetNuGetProjects().Any();
            });
        }
    }
}
