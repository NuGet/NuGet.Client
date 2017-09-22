// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging.Core;

namespace NuGet.Packaging.Signing
{
    public class SignatureException : PackagingException
    {
        public SignatureException(string message) : base(message)
        {
        }
    }
}
