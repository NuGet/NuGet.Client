// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Packaging.Core;

namespace NuGet.Packaging.Signing
{
    public class SignatureException : PackagingException
    {

        /// <summary>
        /// Individual trust results.
        /// </summary>
        public IReadOnlyList<SignatureVerificationResult> Results { get; }

        public PackageIdentity PackageIdentity { get; }

        public SignatureException(string message) : base(message)
        {
        }

        public SignatureException(IReadOnlyList<SignatureVerificationResult> results, PackageIdentity package)
            : base(string.Empty)
        {
            Results = results;
            PackageIdentity = package;
        }
    }
}
