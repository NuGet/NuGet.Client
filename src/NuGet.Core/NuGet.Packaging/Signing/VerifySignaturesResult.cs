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
        /// Individual trust results.
        /// </summary>
        public IReadOnlyList<SignatureVerificationResult> Results { get; }

        public string ErrorMessage { get; }

        public VerifySignaturesResult(bool valid, string errorMessage)
            : this(valid, errorMessage, results: Enumerable.Empty<SignatureVerificationResult>())
        {
        }

        public VerifySignaturesResult(bool valid, string errorMessage, IEnumerable<SignatureVerificationResult> results)
        {
            Valid = valid;
            ErrorMessage = errorMessage;
            Results = results?.ToList().AsReadOnly() ?? throw new ArgumentNullException(nameof(results));
        }
    }
}
