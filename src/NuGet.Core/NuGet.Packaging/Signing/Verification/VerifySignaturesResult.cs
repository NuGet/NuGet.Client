// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Collection of signature verification results.
    /// </summary>
    public sealed class VerifySignaturesResult
    {
        /// <summary>
        /// True if signature is valid.
        /// </summary>
        public bool Valid { get; }

        /// <summary>
        /// True if the package is signed.
        /// </summary>
        public bool Signed { get; }

        /// <summary>
        /// Individual trust results.
        /// </summary>
        public IReadOnlyList<PackageVerificationResult> Results { get; }

        public VerifySignaturesResult(bool valid, bool signed)
            : this(valid, signed, results: Enumerable.Empty<PackageVerificationResult>())
        {
        }

        public VerifySignaturesResult(bool valid, bool signed, IEnumerable<PackageVerificationResult> results)
        {
            Valid = valid;
            Signed = signed;
            Results = results?.ToList().AsReadOnly() ?? throw new ArgumentNullException(nameof(results));
        }
    }
}
