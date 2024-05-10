// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Commands;

namespace NuGet.CommandLine
{
    [Serializable]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public sealed class ExitCodeException : CommandException
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public ExitCodeException(int exitCode)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            ExitCode = exitCode;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public int ExitCode { get; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}
