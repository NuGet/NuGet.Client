// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Windows.Input;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace NuGetVSExtension
{
    /// <summary>
    /// UI command to show output window switched to PM output.
    /// Allows binding to any control.
    /// </summary>
    internal sealed class ShowErrorsCommand : ICommand
    {
        private readonly IVsOutputWindow _vsOutputWindow;
        private readonly IVsUIShell _vsUiShell;

        public ShowErrorsCommand(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }
            // get all services we need for displaytion and activation of the NuGet output pane
            _vsOutputWindow = (IVsOutputWindow)serviceProvider.GetService(typeof(SVsOutputWindow));
            _vsUiShell = (IVsUIShell)serviceProvider.GetService(typeof(SVsUIShell));
        }

        // Actually never raised
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        // False if services were unavailable during instantiation. Never change.
        public bool CanExecute(object parameter)
        {
            return _vsUiShell != null && _vsOutputWindow != null;
        }

        public void Execute(object parameter)
        {
            IVsWindowFrame toolWindow = null;
            _vsUiShell.FindToolWindow(0, ref GuidList.guidVsWindowKindOutput, out toolWindow);
            toolWindow?.Show();

            IVsOutputWindowPane pane;
            if (_vsOutputWindow.GetPane(ref NuGetConsole.Implementation.GuidList.guidNuGetOutputWindowPaneGuid, out pane) == VSConstants.S_OK)
            {
                pane.Activate();
            }
        }
    }
}
