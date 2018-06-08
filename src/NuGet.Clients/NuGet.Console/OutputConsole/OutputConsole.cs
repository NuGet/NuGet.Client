// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows.Media;
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
    internal sealed class OutputConsole : IConsole, IConsoleDispatcher
    {
        private const int DefaultConsoleWidth = 120;

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

        public int ConsoleWidth => DefaultConsoleWidth;

        public void Write(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Start();

            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                VsOutputWindowPane.OutputStringThreadSafe(text);
            });
        }

        public void Write(string text, Color? foreground, Color? background)
        {
            // the Output window doesn't allow setting text color
            Write(text);
        }

        public void WriteBackspace()
        {
            throw new NotSupportedException();
        }

        public void WriteLine(string text)
        {
            Write(text + Environment.NewLine);
        }

        public void WriteLine(string format, params object[] args)
        {
            WriteLine(string.Format(CultureInfo.CurrentCulture, format, args));
        }

        public void WriteProgress(string operation, int percentComplete)
        {
        }

        public void Activate()
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                _vsUiShell.FindToolWindow(0,
                    ref GuidList.guidVsWindowKindOutput,
                    out var toolWindow);
                toolWindow?.Show();

                VsOutputWindowPane.Activate();
            });
        }

        public void Clear()
        {
            Start();

            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                VsOutputWindowPane.Activate();
                VsOutputWindowPane.Clear();
            });
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
            Clear();
        }

        #endregion IConsoleDispatcher
    }
}
