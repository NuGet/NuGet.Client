// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.VisualStudio;
using NuGetConsole;
using NuGetConsole.Implementation;
using NuGetConsole.Implementation.PowerConsole;

namespace NuGet.Console.TestContract
{
    [Export(typeof(NuGetApexConsoleTestService))]
    public class NuGetApexConsoleTestService
    {
        private Lazy<IWpfConsole> _wpfConsole => new Lazy<IWpfConsole>(GetWpfConsole);
        private static readonly TimeSpan _timeout = TimeSpan.FromMinutes(10);

        private IWpfConsole GetWpfConsole()
        {
            PowerConsoleWindow powershellConsole = null;
            var timer = Stopwatch.StartNew();

            while (powershellConsole?.ActiveHostInfo?.WpfConsole == null)
            {
                try
                {
                    var outputConsoleWindow = ServiceLocator.GetInstance<IPowerConsoleWindow>();
                    powershellConsole = outputConsoleWindow as PowerConsoleWindow;
                }
                catch when (timer.Elapsed < _timeout)
                {
                    // Retry until the console is loaded
                    Thread.Sleep(100);
                }
            }

            return powershellConsole.ActiveHostInfo.WpfConsole;
        }

        public NuGetApexConsoleTestService()
        {
        }

        public ApexTestConsole GetApexTestConsole()
        {
            var consoleWindow = GetPowershellConsole();

            if (consoleWindow == null)
            {
                throw new InvalidOperationException("Unable to get powershell window");
            }

            return new ApexTestConsole(_wpfConsole.Value, consoleWindow);
        }

        private PowerConsoleToolWindow GetPowershellConsole()
        {
            IVsWindowFrame window = null;
            PowerConsoleToolWindow powerConsole = null;
            var powerConsoleToolWindowGUID = new Guid("0AD07096-BBA9-4900-A651-0598D26F6D24");
            var stopwatch = Stopwatch.StartNew();
            var timeout = TimeSpan.FromMinutes(5);

            var uiShell = ServiceLocator.GetInstance<IVsUIShell>();

            // Open PMC in VS
            do
            {
                if (VSConstants.S_OK  == uiShell.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fForceCreate, powerConsoleToolWindowGUID, out window))
                {
                    window.Show();
                }
                else
                {
                    System.Threading.Thread.Sleep(100);
                }
            }
            while (stopwatch.Elapsed < timeout && window == null);

            // Get PowerConsoleToolWindow from the VS frame
            if (window != null)
            {
                if (VSConstants.S_OK == window.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out var toolPane))
                {
                    powerConsole = (PowerConsoleToolWindow)toolPane;
                }
            }

            return powerConsole;
        }
    }
}
