// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security;

namespace NuGet.Common
{
    public class EnvironmentVariableWrapper : IEnvironmentVariableReaderWriter
    {
        public static IEnvironmentVariableReader Instance { get; } = new EnvironmentVariableWrapper();

        public static IEnvironmentVariableReaderWriter ReaderWriter { get; } = new EnvironmentVariableWrapper();

        public string? GetEnvironmentVariable(string variable)
        {
            try
            {
#pragma warning disable RS0030 // Do not used banned APIs (This is the only place where Environment.GetEnvironmentVariable is allowed
                return Environment.GetEnvironmentVariable(variable);
#pragma warning restore RS0030 // Do not used banned APIs
            }
            catch (SecurityException)
            {
                return null;
            }
        }

        public void SetEnvironmentVariable(string name, string? value)
        {
#pragma warning disable RS0030 // Do not use banned APIs
            Environment.SetEnvironmentVariable(name, value);
#pragma warning restore RS0030 // Do not use banned APIs
        }
    }
}
