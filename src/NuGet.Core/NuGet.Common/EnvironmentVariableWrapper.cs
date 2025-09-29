// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security;
using System.Text.RegularExpressions;

namespace NuGet.Common
{
    public class EnvironmentVariableWrapper : IEnvironmentVariableReader
    {
        public static IEnvironmentVariableReader Instance { get; } = new EnvironmentVariableWrapper();

        public string? GetEnvironmentVariable(string variable)
        {
            try
            {
#pragma warning disable RS0030 // Do not use banned APIs
                return Environment.GetEnvironmentVariable(NormalizeSourceName(variable));
#pragma warning restore RS0030 // Do not use banned APIs
            }
            catch (SecurityException)
            {
                return null;
            }
        }

        private static string NormalizeSourceName(string sourceName)
        {
            // Replace invalid env var chars with underscore
            return Regex.Replace(sourceName, @"[^A-Za-z0-9_]", "_");
        }
    }
}
