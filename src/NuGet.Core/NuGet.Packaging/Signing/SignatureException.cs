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

        public NuGetLogCode Code { get; }

        public SignatureException(NuGetLogCode code, string message)
            : base(code, message)
        {
            Code = code;
        }

        public SignatureException(string message)
            : this(NuGetLogCode.NU3000, message)
        {
        }

        public SignatureException(IReadOnlyList<PackageVerificationResult> results, PackageIdentity package)
            : this(string.Empty)
        {
            Results = results;
            PackageIdentity = package;
        }

        public SignatureException(string message, PackageIdentity package)
            : this(message)
        {
            PackageIdentity = package;
        }

        public override string ToString()
        {
            return AsLogMessage().FormatWithCode();
        }
    }
}