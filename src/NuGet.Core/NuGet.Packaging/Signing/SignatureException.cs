// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Common;
using NuGet.Packaging.Core;

namespace NuGet.Packaging.Signing
{
    public class SignatureException : PackagingException
    {

        /// <summary>
        /// Individual trust results.
        /// </summary>
        public IReadOnlyList<PackageVerificationResult> Results { get; }

        public PackageIdentity PackageIdentity { get; }

        public SignatureException(string message)
            : base(message)
        {
        }

        public SignatureException(NuGetLogCode code, string message)
            : base(code, message)
        {
        }

        public SignatureException(IReadOnlyList<PackageVerificationResult> results, PackageIdentity package)
            : base(string.Empty)
        {
            Results = results;
            PackageIdentity = package;
        }

        public SignatureException(string message, PackageIdentity package)
            : base(message)
        {
            PackageIdentity = package;
        }
    }
}
