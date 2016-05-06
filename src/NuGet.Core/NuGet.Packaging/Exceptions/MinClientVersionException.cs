﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Packaging.Core;

namespace NuGet.Packaging
{
    /// <summary>
    /// Custom exception type for a package that has a higher minClientVersion than the current client.
    /// </summary>
    public class MinClientVersionException : PackagingException
    {
        public MinClientVersionException(string message)
            : base(message)
        {
        }
    }
}
