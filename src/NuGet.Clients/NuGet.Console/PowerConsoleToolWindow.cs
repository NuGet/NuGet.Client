// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio;
using NuGetConsole.Implementation.Console;
using NuGetConsole.Implementation.PowerConsole;
using Task = System.Threading.Tasks.Task;

namespace NuGetConsole.Implementation
{
    /// <summary>
    /// This class implements the tool window.
    /// </summary>
    [Guid("0AD07096-BBA9-4900-A651-0598D26F6D24")]
    public sealed class PowerConsoleToolWindow : ToolWindowPane, IOleCommandTarget, IPowerConsoleService
    {
        private JoinableTask _loadTask;
        private const string F1KeywordValuePmc = "VS.NuGet.PackageManager.Console";

        private AsyncLazy<IComponentModel> _componentModelAsyncLazy = new AsyncLazy<IComponentModel>(
            async delegate { return await AsyncServiceProvider.GlobalProvider.GetComponentModelAsync(); }, ThreadHelper.JoinableTaskFactory);

        /// <summary>
        /// Get VS IComponentModel service.
        /// </summary>
        private IComponentModel ComponentModel
        {
            get { return ComponentModelAsyncLazy.GetValue(); }
        }

        private PowerConsoleWindow PowerConsoleWindow
        {
            get { return ComponentModel.GetService<IPowerConsoleWindow>() as PowerConsoleWindow; }
        }

        private bool IsToolbarEnabled
        {
            get
            {
                return _wpfConsole != null &&
                       _wpfConsole.Dispatcher.IsStartCompleted &&
                       _wpfConsole.Host != null &&
                       _wpfConsole.Host.IsCommandEnabled;
            }
        }

        /// <summary>
        /// Standard constructor for the tool window.
        /// </summary>
        public PowerConsoleToolWindow()
            :
                base(null)
        {
            Caption = Resources.ToolWindowTitle;
            BitmapResourceID = 301;
            BitmapIndex = 0;
            ToolBar = new CommandID(GuidList.guidNuGetCmdSet, PkgCmdIDList.idToolbar);
        }

        /// <summary>
        /// Internal for testing, true when the console has been loaded.
        /// </summary>
        public bool IsLoaded { get; private set; }

        [SuppressMessage(
            "Microsoft.Design",
            "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification = "We really don't want exceptions from the console to bring down VS")]
        public override void OnToolWindowCreated()
        {
            base.OnToolWindowCreated();

            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Register key bindings to use in the editor
                var windowFrame = (IVsWindowFrame)Frame;
                if (windowFrame != null)
                {
                    // Set F1 help keyword
                    WindowFrameHelper.AddF1HelpKeyword(windowFrame, keywordValue: F1KeywordValuePmc);
                }

                var cmdUi = VSConstants.GUID_TextEditorFactory;
                windowFrame.SetGuidProperty((int)__VSFPROPID.VSFPROPID_InheritKeyBindings, ref cmdUi);
            });

            // start a task when VS is idle and don't await it immediately
            _loadTask = NuGetUIThreadHelper.JoinableTaskFactory.StartOnIdle(
                async () =>
                {
                    // Load
                    await Task.Run(LoadConsoleEditorAsync);

                    // Mark as complete
                    IsLoaded = true;
                });
        }

        protected override void OnClose()
        {
            base.OnClose();

            if (_wpfConsole != null)
            {
                _wpfConsole.Dispose();
            }
        }

        /// <summary>
        /// This override allows us to forward these messages to the editor instance as well
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        protected override bool PreProcessMessage(ref Message m)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Now, await for the _loadTask which was started in OnToolWindowCreated API
            if (_loadTask != null && _loadTask.IsCompleted)
            {
                var vsWindowPane = VsTextView as IVsWindowPane;
                if (vsWindowPane != null)
                {
                    var pMsg = new MSG[1];
                    pMsg[0].hwnd = m.HWnd;
                    pMsg[0].message = (uint)m.Msg;
                    pMsg[0].wParam = m.WParam;
                    pMsg[0].lParam = m.LParam;

                    return vsWindowPane.TranslateAccelerator(pMsg) == 0;
                }
            }

            return base.PreProcessMessage(ref m);
        }

        /// <summary>
        /// Override to forward to editor or handle accordingly if supported by this tool window.
        /// </summary>
        int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            // examine buttons within our toolbar
            if (pguidCmdGroup == GuidList.guidNuGetCmdSet)
            {
                var isEnabled = IsToolbarEnabled;

                if (isEnabled)
                {
                    var isStopButton = (prgCmds[0].cmdID == 0x0600); // 0x0600 is the Command ID of the Stop button, defined in .vsct

                    // when command is executing: enable stop button and disable the rest
                    // when command is not executing: disable the stop button and enable the rest
                    isEnabled = !isStopButton ^ WpfConsole.Dispatcher.IsExecutingCommand;
                }

                if (isEnabled)
                {
                    prgCmds[0].cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED);
                }
                else
                {
                    prgCmds[0].cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED);
                }

                return VSConstants.S_OK;
            }

            var hr = OleCommandFilter.OLECMDERR_E_NOTSUPPORTED;

            if (VsTextView != null)
            {
                var cmdTarget = (IOleCommandTarget)VsTextView;
                hr = cmdTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
            }

            if (hr == OleCommandFilter.OLECMDERR_E_NOTSUPPORTED
                ||
                hr == OleCommandFilter.OLECMDERR_E_UNKNOWNGROUP)
            {
                Guid copyPguildCmdGroup = pguidCmdGroup;
                ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    hr = await QueryStatusInServiceAsync(copyPguildCmdGroup, cCmds, prgCmds, pCmdText);
                });
            }

            return hr;
        }

        private async System.Threading.Tasks.Task<int> QueryStatusInServiceAsync(Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            int hr = OleCommandFilter.OLECMDERR_E_UNKNOWNGROUP;
            var target = await AsyncServiceProvider.GlobalProvider.GetServiceAsync<IOleCommandTarget>();
            if (target != null)
            {
                hr = target.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
            }

            return hr;
        }


        /// <summary>
        /// Override to forward to editor or handle accordingly if supported by this tool window.
        /// </summary>
        int IOleCommandTarget.Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            var hr = OleCommandFilter.OLECMDERR_E_NOTSUPPORTED;

            if (VsTextView != null)
            {
                var cmdTarget = (IOleCommandTarget)VsTextView;
                hr = cmdTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            }

            if (hr == OleCommandFilter.OLECMDERR_E_NOTSUPPORTED
                ||
                hr == OleCommandFilter.OLECMDERR_E_UNKNOWNGROUP)
            {
                Guid copyPguildCmdGroup = pguidCmdGroup;
                ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    hr = await ExecInServiceAsync(copyPguildCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                });
            }

            return hr;
        }

        private async System.Threading.Tasks.Task<int> ExecInServiceAsync(Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            int hr = OleCommandFilter.OLECMDERR_E_UNKNOWNGROUP;
            var target = await AsyncServiceProvider.GlobalProvider.GetServiceAsync<IOleCommandTarget>();
            if (target != null)
            {
                hr = target.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            }

            return hr;
        }

        private HostInfo ActiveHostInfo
        {
            get { return PowerConsoleWindow.ActiveHostInfo; }
        }

        private async Task LoadConsoleEditorAsync()
        {
            try
            {
                if (WpfConsole != null)
                {
                    // allow the console to start writing output
                    WpfConsole.StartWritingOutput();

                    var consolePane = WpfConsole.Content as FrameworkElement;

                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    ConsoleParentPane.AddConsoleEditor(consolePane);

                    // WPF doesn't handle input focus automatically in this scenario. We
                    // have to set the focus manually, otherwise the editor is displayed but
                    // not focused and not receiving keyboard inputs until clicked.
                    if (consolePane != null)
                    {
                        PendingMoveFocus(consolePane);
                    }
                }
            }
            catch (Exception x)
            {
                ExceptionHelper.WriteErrorToActivityLog(x);
            }
        }

        /// <summary>
        /// Set pending focus to a console pane. At the time of setting active host,
        /// the pane (UIElement) is usually not loaded yet and can't receive focus.
        /// In this case, we need to set focus in its Loaded event.
        /// </summary>
        /// <param name="consolePane"></param>
        private void PendingMoveFocus(FrameworkElement consolePane)
        {
            if (consolePane.IsLoaded
                && PresentationSource.FromDependencyObject(consolePane) != null)
            {
                PendingFocusPane = null;
                MoveFocus(consolePane);
            }
            else
            {
                PendingFocusPane = consolePane;
            }
        }

        private FrameworkElement _pendingFocusPane;

        private FrameworkElement PendingFocusPane
        {
            get { return _pendingFocusPane; }
            set
            {
                if (_pendingFocusPane != null)
                {
                    _pendingFocusPane.Loaded -= PendingFocusPane_Loaded;
                }
                _pendingFocusPane = value;
                if (_pendingFocusPane != null)
                {
                    _pendingFocusPane.Loaded += PendingFocusPane_Loaded;
                }
            }
        }

        private void PendingFocusPane_Loaded(object sender, RoutedEventArgs e)
        {
            MoveFocus(PendingFocusPane);
            PendingFocusPane = null;
        }

        private void MoveFocus(FrameworkElement consolePane)
        {
            // TAB focus into editor (consolePane.Focus() does not work due to editor layouts)
            consolePane.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));

            // Try start the console session now. This needs to be after the console
            // pane getting focus to avoid incorrect initial editor layout.
            StartConsoleSession(consolePane);
        }

        [SuppressMessage(
            "Microsoft.Design",
            "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification = "We really don't want exceptions from the console to bring down VS")]
        private void StartConsoleSession(FrameworkElement consolePane)
        {
            if (WpfConsole != null
                && WpfConsole.Content == consolePane
                && WpfConsole.Host != null)
            {
                try
                {
                    if (WpfConsole.Dispatcher.IsStartCompleted)
                    {
                        OnDispatcherStartCompleted();
                        // if the dispatcher was started before we reach here,
                        // it means the dispatcher has been in read-only mode (due to _startedWritingOutput = false).
                        // enable key input now.
                        WpfConsole.Dispatcher.AcceptKeyInput();
                    }
                    else
                    {
                        WpfConsole.Dispatcher.StartCompleted += (sender, args) => OnDispatcherStartCompleted();
                        WpfConsole.Dispatcher.StartWaitingKey += OnDispatcherStartWaitingKey;
                        WpfConsole.Dispatcher.Start();
                    }
                }
                catch (Exception x)
                {
                    // hide the text "initialize host" when an error occurs.
                    ConsoleParentPane.NotifyInitializationCompleted();

                    NuGetUIThreadHelper.JoinableTaskFactory.Run(() => WpfConsole.WriteLineAsync(x.GetBaseException().ToString()));
                    ExceptionHelper.WriteErrorToActivityLog(x);
                }
            }
            else
            {
                ConsoleParentPane.NotifyInitializationCompleted();
            }
        }

        private void OnDispatcherStartWaitingKey(object sender, EventArgs args)
        {
            WpfConsole.Dispatcher.StartWaitingKey -= OnDispatcherStartWaitingKey;
            // we want to hide the text "initialize host..." when waiting for key input
            ConsoleParentPane.NotifyInitializationCompleted();
        }

        private void OnDispatcherStartCompleted()
        {
            WpfConsole.Dispatcher.StartWaitingKey -= OnDispatcherStartWaitingKey;

            ConsoleParentPane.NotifyInitializationCompleted();

            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var shellSvc = await AsyncServiceProvider.GlobalProvider.GetServiceAsync<IVsUIShell>();
                // force the UI to update the toolbar
                shellSvc.UpdateCommandUI(fImmediateUpdate: 0 /* false = update UI asynchronously */);
            });

            NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.PackageManagerConsoleLoaded);
        }

        private IWpfConsole _wpfConsole;

        /// <summary>
        /// Get the WpfConsole of the active host.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private IWpfConsole WpfConsole
        {
            get
            {
                if (_wpfConsole == null)
                {
                    _wpfConsole = ActiveHostInfo.WpfConsole;
                }

                return _wpfConsole;
            }
        }

        private IVsTextView _vsTextView;

        /// <summary>
        /// Get the VsTextView of current WpfConsole if exists.
        /// </summary>
        private IVsTextView VsTextView
        {
            get
            {
                if (_vsTextView == null
                    && _wpfConsole != null)
                {
                    _vsTextView = (IVsTextView)(WpfConsole.VsTextView);
                }
                return _vsTextView;
            }
        }

        private ConsoleContainer _consoleParentPane;

        /// <summary>
        /// Get the parent pane of console panes. This serves as the Content of this tool window.
        /// </summary>
        private ConsoleContainer ConsoleParentPane
        {
            get
            {
                if (_consoleParentPane == null)
                {
                    _consoleParentPane = new ConsoleContainer();
                }
                return _consoleParentPane;
            }
        }

        public override object Content
        {
            get { return ConsoleParentPane; }
            set { base.Content = value; }
        }

        #region IPowerConsoleService Region

        public event EventHandler ExecuteEnd;
        private ITextSnapshot _snapshot;
        private int _previousPosition;

        public bool Execute(string command, object[] inputs)
        {
            if (ConsoleStatus.IsBusy)
            {
                NuGetUIThreadHelper.JoinableTaskFactory.Run(() => VSOutputConsole.WriteLineAsync(Resources.PackageManagerConsoleBusy));
                throw new NotSupportedException(Resources.PackageManagerConsoleBusy);
            }

            if (!string.IsNullOrEmpty(command))
            {
                WpfConsole.SetExecutionMode(true);
                // Cast the ToolWindowPane to PowerConsoleToolWindow
                // Access the IHost from PowerConsoleToolWindow as follows PowerConsoleToolWindow.WpfConsole.Host
                // Cast IHost to IAsyncHost
                // Also, register for IAsyncHost.ExecutedEnd and return only when the command is completed
                var powerShellConsole = (IPrivateWpfConsole)WpfConsole;
                var host = powerShellConsole.Host;

                var asynchost = host as IAsyncHost;
                if (asynchost != null)
                {
                    asynchost.ExecuteEnd += PowerConsoleCommand_ExecuteEnd;
                }

                // Here, we store the snapshot of the powershell Console output text buffer
                // Snapshot has reference to the buffer and the current length of the buffer
                // And, upon execution of the command, (check the commandexecuted handler)
                // the changes to the buffer is identified and copied over to the VS output window
                if (powerShellConsole.InputLineStart != null
                    && powerShellConsole.InputLineStart.Value.Snapshot != null)
                {
                    _snapshot = powerShellConsole.InputLineStart.Value.Snapshot;
                }

                // We should write the command to the console just to imitate typical user action before executing it
                // Asserts get fired otherwise. Also, the log is displayed in a disorderly fashion
                NuGetUIThreadHelper.JoinableTaskFactory.Run(() => powerShellConsole.WriteLineAsync(command));

                return host.Execute(powerShellConsole, command, null);
            }
            return false;
        }

        public void StartDispatcher()
        {
            if (WpfConsole != null)
            {
                WpfConsole.Dispatcher.Start();
            }
        }

        public bool IsHostSuccessfullyInitialized()
        {
            if (WpfConsole != null)
            {
                return WpfConsole.Host.IsInitializedSuccessfully;
            }
            return false;
        }

        private void PowerConsoleCommand_ExecuteEnd(object sender, EventArgs e)
        {
            // Flush the change in console text buffer onto the output window for testability
            // If the VSOutputConsole could not be obtained, just ignore
            if (VSOutputConsole != null
                && _snapshot != null)
            {
                if (_previousPosition < _snapshot.Length)
                {
                    NuGetUIThreadHelper.JoinableTaskFactory.Run(() => VSOutputConsole.WriteLineAsync(_snapshot.GetText(_previousPosition, (_snapshot.Length - _previousPosition))));
                }
                _previousPosition = _snapshot.Length;
            }

            (sender as IAsyncHost).ExecuteEnd -= PowerConsoleCommand_ExecuteEnd;
            WpfConsole.SetExecutionMode(false);

            // This does NOT imply that the command succeeded. It just indicates that the console is ready for input now
            NuGetUIThreadHelper.JoinableTaskFactory.Run(() => VSOutputConsole.WriteLineAsync(Resources.PackageManagerConsoleCommandExecuted));
            ExecuteEnd.Raise(this, EventArgs.Empty);
        }

        private IOutputConsole _vsOutputConsole;

        private IOutputConsole VSOutputConsole
        {
            get
            {
                if (_vsOutputConsole == null)
                {
                    _vsOutputConsole = NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
                    {
                        var consoleProvider = await ServiceLocator.GetInstanceAsync<IOutputConsoleProvider>();
                        if (null != consoleProvider)
                        {
                            return await consoleProvider.CreatePackageManagerConsoleAsync();
                        }
                        return null;
                    });
                }
                return _vsOutputConsole;
            }
        }

        private IConsoleStatus _consoleStatus;

        private IConsoleStatus ConsoleStatus
        {
            get
            {
                if (_consoleStatus == null)
                {
                    _consoleStatus = ServiceLocator.GetInstance<IConsoleStatus>();
                    Debug.Assert(_consoleStatus != null);
                }

                return _consoleStatus;
            }
        }

        public AsyncLazy<IComponentModel> ComponentModelAsyncLazy { get => _componentModelAsyncLazy; set => _componentModelAsyncLazy = value; }

        #endregion
    }
}
