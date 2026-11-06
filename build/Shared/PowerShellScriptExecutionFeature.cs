// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;

namespace NuGet.Shared
{
    internal static class PowerShellScriptExecutionFeature
    {
        internal const string SwitchName = "NuGet.ControlPowerShellScriptExecution";
        internal const string EnvironmentVariable = "NUGET_CONTROL_POWERSHELL_SCRIPT_EXECUTION";

        internal static bool IsEnabled => IsEnabledCore(EnvironmentVariableWrapper.Instance);

        internal static bool IsEnabledCore(IEnvironmentVariableReader environmentVariableReader)
        {
            string? value = environmentVariableReader.GetEnvironmentVariable(EnvironmentVariable);
            if (value == "0" || value == "1")
            {
                return value == "1";
            }

            return AppContext.TryGetSwitch(SwitchName, out bool enabled) && enabled;
        }
    }
}
