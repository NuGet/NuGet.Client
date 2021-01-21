// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.VisualStudio.Telemetry.Powershell
{
    public static class NuGetPowerShellUsage
    {
        public delegate void SolutionOpenHandler();
        public static event SolutionOpenHandler SolutionOpenEvent;

        public delegate void SolutionCloseHandler();
        public static event SolutionCloseHandler SolutionCloseEvent;

        public delegate void VSInstanceCloseHandler();
        public static event VSInstanceCloseHandler VSInstanceCloseEvent;

        public delegate void PowerShellLoadEventHandler(bool isPMC);
        public static event PowerShellLoadEventHandler PowerShellLoadEvent;

        public delegate void PowerShellCommandExecuteEventHandler(bool isPMC, string commandStr);
        public static event PowerShellCommandExecuteEventHandler PowerShellCommandExecuteEvent;

        public delegate void InitPs1LoadEventHandler(bool isPMC);
        public static event InitPs1LoadEventHandler InitPs1LoadEvent;

        public delegate void PMCWindowEventHandler(bool isLoad);
        public static event PMCWindowEventHandler PMCWindowsEvent;

        public static void RaisePowerShellLoadEvent(bool isPMC)
        {
            PowerShellLoadEvent?.Invoke(isPMC);
        }

        public static void RaiseCommandExecuteEvent(bool isPMC, string commandStr)
        {
            PowerShellCommandExecuteEvent?.Invoke(isPMC, commandStr);
        }

        public static void RaisInitPs1LoadEvent(bool isPMC)
        {
            InitPs1LoadEvent?.Invoke(isPMC);
        }

        public static void RaisePMCWindowsLoadEvent(bool isLoad)
        {
            PMCWindowsEvent?.Invoke(isLoad);
        }

        public static void RaiseSolutionOpenEvent()
        {
            SolutionOpenEvent?.Invoke();
        }

        public static void RaiseSolutionCloseEvent()
        {
            SolutionCloseEvent?.Invoke();
        }

        public static void RaiseVSInstanceCloseEvent()
        {
            VSInstanceCloseEvent?.Invoke();
        }
    }
}
