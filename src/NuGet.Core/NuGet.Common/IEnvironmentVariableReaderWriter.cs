// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Common
{
    public interface IEnvironmentVariableReaderWriter : IEnvironmentVariableReader
    {
#pragma warning disable RS0030 // Do not use banned APIs
        /// <inheritdoc cref="System.Environment.SetEnvironmentVariable(string, string)"/>
#pragma warning restore RS0030 // Do not use banned APIs
        void SetEnvironmentVariable(string name, string? value);
    }
}
