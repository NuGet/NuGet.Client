// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Win32;

namespace NuGetConsole.Host
{
    internal static class RegistryHelper
    {
        /// <summary>
        /// Detects if PowerShell 2.0 runtime is installed.
        /// </summary>
        /// <remarks>
        /// Detection logic is obtained from here:
        /// http://blogs.msdn.com/b/powershell/archive/2009/06/25/detection-logic-poweshell-installation.aspx
        /// </remarks>
        public static bool CheckIfPowerShell2OrAboveInstalled()
        {
            // PS 1.0 and 2.0 is set under "...\PowerShell\1" key
            string keyValue = GetSubKeyValue(@"SOFTWARE\Microsoft\PowerShell\1\PowerShellEngine", "PowerShellVersion");
            if ("2.0".Equals(keyValue, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // PS 3.0 and 4.0 is set under "...\PowerShell\3" key
            keyValue = GetSubKeyValue(@"SOFTWARE\Microsoft\PowerShell\3\PowerShellEngine", "PowerShellVersion");
            if ("3.0".Equals(keyValue, StringComparison.OrdinalIgnoreCase)
                ||
                "4.0".Equals(keyValue, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static string GetSubKeyValue(string keyPath, string valueName)
        {
            RegistryKey currentKey = Registry.LocalMachine;

            foreach (string subKeyName in keyPath.Split('\\'))
            {
                currentKey = currentKey.OpenSubKey(subKeyName);
                if (currentKey == null)
                {
                    return null;
                }
            }

            return (string)currentKey.GetValue(valueName);
        }
    }
}
