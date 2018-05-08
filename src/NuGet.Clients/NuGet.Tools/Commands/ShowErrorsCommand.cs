// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.VisualStudio;

namespace NuGetVSExtension
{
    /// <summary>
    /// UI command to show output window switched to PM output.
    /// Allows binding to any control.
    /// </summary>
    internal sealed class ShowErrorsCommand : ICommand
    {
        private readonly Lazy<IVsOutputWindow> _vsOutputWindow;
        private readonly Lazy<IVsUIShell> _vsUiShell;

        [SuppressMessage("Microsoft.VisualStudio.Threading.Analyzers", "VSTHRD010", Justification = "NuGet/Home#4833 Baseline")]
        public ShowErrorsCommand(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }
            // get all services we need for display and activation of the NuGet output pane
            _vsOutputWindow = new Lazy<IVsOutputWindow>(() =>
            {
                return NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    return (IVsOutputWindow)serviceProvider.GetService(typeof(SVsOutputWindow));
                });
            });

            _vsUiShell = new Lazy<IVsUIShell>(() =>
            {
                return NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    return (IVsUIShell)serviceProvider.GetService(typeof(SVsUIShell));
                });
            });
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
            return _vsUiShell?.Value != null && _vsOutputWindow?.Value != null;
        }

        [SuppressMessage("Microsoft.VisualStudio.Threading.Analyzers", "VSTHRD010", Justification = "NuGet/Home#4833 Baseline")]
        public void Execute(object parameter)
        {
            IVsWindowFrame toolWindow = null;
            _vsUiShell.Value.FindToolWindow(0, ref GuidList.guidVsWindowKindOutput, out toolWindow);
            toolWindow?.Show();

            IVsOutputWindowPane pane;
            if (_vsOutputWindow.Value.GetPane(ref NuGetConsole.GuidList.guidNuGetOutputWindowPaneGuid, out pane) == VSConstants.S_OK)
            {
                pane.Activate();
            }
        }
    }
}
