// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Windows.Input;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NuGet.VisualStudio;

namespace NuGetVSExtension
{
    /// <summary>
    /// UI command to show output window switched to PM output.
    /// Allows binding to any control.
    /// </summary>
    internal sealed class ShowErrorsCommand : ICommand
    {
        private readonly AsyncLazy<IVsOutputWindow> _vsOutputWindow;
        private readonly AsyncLazy<IVsUIShell> _vsUiShell;

        public ShowErrorsCommand()
        {
            // get all services we need for display and activation of the NuGet output pane
            _vsOutputWindow = new AsyncLazy<IVsOutputWindow>(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                return await AsyncServiceProvider.GlobalProvider.GetServiceAsync<IVsOutputWindow>();
            },
            NuGetUIThreadHelper.JoinableTaskFactory);

            _vsUiShell = new AsyncLazy<IVsUIShell>(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                return await AsyncServiceProvider.GlobalProvider.GetServiceAsync<IVsUIShell>();
            },
            NuGetUIThreadHelper.JoinableTaskFactory);
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
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                return await _vsUiShell?.GetValueAsync() != null && _vsOutputWindow?.GetValueAsync() != null;
            });
        }

        public void Execute(object parameter)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                IVsWindowFrame toolWindow = null;
                IVsUIShell vsUiShell = await _vsUiShell.GetValueAsync();
                vsUiShell.FindToolWindow(0, ref GuidList.guidVsWindowKindOutput, out toolWindow);
                toolWindow?.Show();

                IVsOutputWindowPane pane;
                IVsOutputWindow vsOutputWindow = await _vsOutputWindow.GetValueAsync();
                if (vsOutputWindow.GetPane(ref NuGetConsole.GuidList.guidNuGetOutputWindowPaneGuid, out pane) == VSConstants.S_OK)
                {
                    pane.Activate();
                }
            });
        }
    }
}
