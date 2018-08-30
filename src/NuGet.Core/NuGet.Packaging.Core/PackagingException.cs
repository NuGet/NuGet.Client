// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Packaging.Core
{
    /// <summary>
    /// Generic packaging exception.
    /// </summary>
    public class PackagingException : Exception
    {
        public PackagingException(string message)
            : base(message)
        {
        }

        public PackagingException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
