// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text.Editor;
using NuGet.VisualStudio;
using NuGetConsole;
using NuGetConsole.Implementation;
using NuGetConsole.Implementation.Console;

namespace NuGet.Console.TestContract
{
    public class ApexTestConsole
    {
        private IWpfConsole _wpfConsole;
        private PowerConsoleToolWindow _consoleWindow;

        public ApexTestConsole(IWpfConsole WpfConsole, PowerConsoleToolWindow consoleWindow)
        {
            _wpfConsole = WpfConsole ?? throw new ArgumentNullException(nameof(WpfConsole));
            _consoleWindow = consoleWindow ?? throw new ArgumentNullException(nameof(consoleWindow));
        }

        private bool EnsureInitilizeConsole()
        {
            var stopwatch = Stopwatch.StartNew();
            var timeout = TimeSpan.FromMinutes(5);
            var loaded = false;

            do
            {
                // Avoid getting the host until the window has loaded
                // If the host is loaded first the window will hang
                if (_consoleWindow.IsLoaded)
                {
                    UIInvoke(() =>
                    {
                        if (_wpfConsole.Dispatcher.IsStartCompleted && _wpfConsole.Host != null)
                        {
                            loaded = true;
                        }
                    });
                }

                if (!loaded)
                {
                    System.Threading.Thread.Sleep(100);
                }
            }
            while (!loaded && stopwatch.Elapsed < timeout);

            return loaded;
        }

        public bool ConsoleContainsMessage(string message)
        {
            if (!EnsureInitilizeConsole())
            {
                return false;
            }

            var snapshot = (_wpfConsole.Content as IWpfTextViewHost).TextView.TextBuffer.CurrentSnapshot;
            for (var i = 0; i < snapshot.LineCount; i++)
            {
                var snapshotLine = snapshot.GetLineFromLineNumber(i);
                var lineText = snapshotLine.GetText();

                var foundMessage = lineText.Contains(message);
                if (foundMessage)
                {
                    return true;
                }
            }
            return false;
        }

        public void Clear()
        {
            if (!EnsureInitilizeConsole())
            {
                return;
            }

            UIInvoke(() => _wpfConsole.Clear());
        }

        public bool RunCommand(string command, TimeSpan timeout)
        {
            return WaitForActionComplete(() => RunCommandWithoutWait(command), timeout);
        }

        public void RunCommandWithoutWait(string command)
        {
            if (!string.IsNullOrEmpty(command))
            {
                UIInvoke(async () =>
                {
                    var wpfHost = _wpfConsole.Host;
                    if (wpfHost.IsCommandEnabled)
                    {
                        _wpfConsole.WriteLine(command);
                        await Task.Run(() => wpfHost.Execute(_wpfConsole, command, null));
                    }
                });
            }
        }

        public bool WaitForActionComplete(Action action, TimeSpan timeout)
        {
            if (!EnsureInitilizeConsole())
            {
                return false;
            }

            using (var semaphore = new ManualResetEventSlim())
            {
                void eventHandler(object s, EventArgs e) => semaphore.Set();
                var dispatcher = (IPrivateConsoleDispatcher)_wpfConsole.Dispatcher;
                dispatcher.SetExecutingCommand(true);
                var asynchost = (IAsyncHost)_wpfConsole.Host;
                asynchost.ExecuteEnd += eventHandler;

                try
                {
                    // Run
                    action();

                    return semaphore.Wait(timeout);
                }
                finally
                {
                    asynchost.ExecuteEnd -= eventHandler;
                    dispatcher.SetExecutingCommand(false);
                }
            }
        }

        private void UIInvoke(Action action)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                action();
            });
        }
    }
}
