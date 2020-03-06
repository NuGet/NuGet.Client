// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows.Media;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NuGet.VisualStudio;

namespace NuGetConsole
{
    /// <summary>
    /// This class implements the IConsole interface in order to integrate with the PowerShellHost.
    /// It sends PowerShell host outputs to the VS Output tool window.
    /// </summary>
    internal sealed class OutputConsole : SharedOutputConsole, IConsole, IConsoleDispatcher
    {
        private readonly IVsOutputWindow _vsOutputWindow;
        private readonly IVsUIShell _vsUiShell;
        private readonly AsyncLazy<IVsOutputWindowPane> _outputWindowPane;

        private IVsOutputWindowPane VsOutputWindowPane => NuGetUIThreadHelper.JoinableTaskFactory.Run(_outputWindowPane.GetValueAsync);

        public OutputConsole(
            IVsOutputWindow vsOutputWindow,
            IVsUIShell vsUiShell)
        {
            if (vsOutputWindow == null)
            {
                throw new ArgumentNullException(nameof(vsOutputWindow));
            }

            if (vsUiShell == null)
            {
                throw new ArgumentNullException(nameof(vsUiShell));
            }

            _vsOutputWindow = vsOutputWindow;
            _vsUiShell = vsUiShell;

            _outputWindowPane = new AsyncLazy<IVsOutputWindowPane>(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // create the Package Manager pane within the Output window
                var hr = _vsOutputWindow.CreatePane(
                    ref GuidList.guidNuGetOutputWindowPaneGuid,
                    Resources.OutputConsolePaneName,
                    fInitVisible: 1,
                    fClearWithSolution: 0);
                ErrorHandler.ThrowOnFailure(hr);

                IVsOutputWindowPane pane;
                hr = _vsOutputWindow.GetPane(
                    ref GuidList.guidNuGetOutputWindowPaneGuid,
                    out pane);
                ErrorHandler.ThrowOnFailure(hr);

                return pane;

            }, NuGetUIThreadHelper.JoinableTaskFactory);
        }

        #region IConsole

        public IHost Host { get; set; }

        public bool ShowDisclaimerHeader => false;

        public IConsoleDispatcher Dispatcher => this;

        public override async Task WriteAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Start();

            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            VsOutputWindowPane.OutputStringThreadSafe(text);
        }

        public override async Task ActivateAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            _vsUiShell.FindToolWindow(0,
                ref GuidList.guidVsWindowKindOutput,
                out var toolWindow);
            toolWindow?.Show();

            VsOutputWindowPane.Activate();
        }

        public override async Task ClearAsync()
        {
            Start();

            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            VsOutputWindowPane.Activate();
            VsOutputWindowPane.Clear();
        }

        #endregion IConsole

        #region IConsoleDispatcher

        public void Start()
        {
            if (!IsStartCompleted)
            {
                var ignore = VsOutputWindowPane;
                StartCompleted?.Invoke(this, EventArgs.Empty);
            }

            IsStartCompleted = true;
        }

        public event EventHandler StartCompleted;

        event EventHandler IConsoleDispatcher.StartWaitingKey
        {
            add { }
            remove { }
        }

        public bool IsStartCompleted { get; private set; }

        public bool IsExecutingCommand
        {
            get { return false; }
        }

        public bool IsExecutingReadKey
        {
            get { throw new NotSupportedException(); }
        }

        public bool IsKeyAvailable
        {
            get { throw new NotSupportedException(); }
        }

        public void AcceptKeyInput()
        {
        }

        public VsKeyInfo WaitKey()
        {
            throw new NotSupportedException();
        }

        public void ClearConsole()
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(() => ClearAsync());
        }

        #endregion IConsoleDispatcher
    }
}
