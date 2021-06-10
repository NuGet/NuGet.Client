// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security;
using Microsoft.Win32;
using NuGet.Common;

namespace NuGet.CommandLine
{
    public static class RegistryKeyUtility
    {
        /// <summary>
        /// Gets a value from a Windows Registry
        /// </summary>
        /// <param name="name">Registry key name</param>
        /// <param name="registryKeyPath">Registry key path</param>
        /// <param name="registryKey">Windows Registry Key object</param>
        /// <param name="logger">Logger to log errors</param>
        /// <returns>Registry value or null if there is an error</returns>
        public static object GetValueFromRegistryKey(string name, string registryKeyPath, RegistryKey registryKey, ILogger logger)
        {
            try
            {
                using (var key = registryKey?.OpenSubKey(registryKeyPath))
                {
                    var result = key?.GetValue(name);

                    return result;
                }
            }
            catch (SecurityException ex)
            {
                // If the user doesn't have access to the registry, then we'll return null
                ExceptionUtilities.LogException(ex, logger);
                return null;
            }
        }
    }
}
