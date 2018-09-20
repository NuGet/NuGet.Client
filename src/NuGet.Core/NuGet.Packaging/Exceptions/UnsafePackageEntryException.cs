// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using NuGet.Common;
using NuGet.Packaging.Core;
namespace NuGet.Packaging
{
    public class UnsafePackageEntryException : PackagingException
    {
        public UnsafePackageEntryException(string message) :
            base(message)
        {
        }

        public UnsafePackageEntryException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}