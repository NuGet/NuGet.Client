// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Windows.Media;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace NuGetConsole
{
    /// <summary>
    /// This class implements the IConsole interface in order to integrate with the PowerShellHost.
    /// It sends PowerShell host outputs to the VS Output tool window.
    /// </summary>
    internal class OutputConsole : IConsole, IConsoleDispatcher
    {
        // guid for our Output window pane
        private static Guid _outputWindowPaneGuid = new Guid("CEC55EC8-CC51-40E7-9243-57B87A6F6BEB");

        private readonly IVsOutputWindow _outputWindow;
        private IVsOutputWindowPane _outputWindowPane;

        public OutputConsole(IVsOutputWindow outputWindow)
        {
            if (outputWindow == null)
            {
                throw new ArgumentNullException("outputWindow");
            }

            _outputWindow = outputWindow;
        }

        public event EventHandler StartCompleted;

        event EventHandler IConsoleDispatcher.StartWaitingKey
        {
            add { }
            remove { }
        }

        public bool IsStartCompleted { get; private set; }

        public IHost Host { get; set; }

        public bool ShowDisclaimerHeader
        {
            get { return false; }
        }

        public IConsoleDispatcher Dispatcher
        {
            get { return this; }
        }

        public int ConsoleWidth
        {
            get { return 120; }
        }

        public void Write(string text)
        {
            if (String.IsNullOrEmpty(text))
            {
                return;
            }

            Start();

            _outputWindowPane.OutputStringThreadSafe(text);
        }

        public void WriteLine(string text)
        {
            Write(text + Environment.NewLine);
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

        public void WriteProgress(string operation, int percentComplete)
        {
        }

        public VsKeyInfo WaitKey()
        {
            throw new NotSupportedException();
        }

        public void Clear()
        {
            Start();

            _outputWindowPane.Activate();
            _outputWindowPane.Clear();
        }

        public void Start()
        {
            if (_outputWindowPane == null)
            {
                // create the Package Manager pane within the Output window
                int result = _outputWindow.CreatePane(ref _outputWindowPaneGuid, Resources.OutputConsolePaneName, fInitVisible: 1, fClearWithSolution: 0);
                if (result == VSConstants.S_OK)
                {
                    result = _outputWindow.GetPane(ref _outputWindowPaneGuid, out _outputWindowPane);

                    Debug.Assert(result == VSConstants.S_OK);
                    Debug.Assert(_outputWindowPane != null);
                }
            }

            if (StartCompleted != null)
            {
                StartCompleted(this, EventArgs.Empty);
            }

            IsStartCompleted = true;
        }

        public void ClearConsole()
        {
            Clear();
        }

        public void AcceptKeyInput()
        {
        }
    }
}
