// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Represents a signature check result and any additional information
    /// needed to display to the user.
    /// </summary>
    public class SignatureVerificationResult
    {
        /// <summary>
        /// Trust result
        /// </summary>
        public SignatureVerificationStatus Trust { get; }

        /// <summary>
        /// Signature
        /// </summary>
        public Signature Signature { get; }

        /// <summary>
        /// Certificate chain resolved.
        /// </summary>
        public X509Chain Chain { get; }

        /// <summary>
        /// SignatureVerificationResult
        /// </summary>
        public SignatureVerificationResult(SignatureVerificationStatus trust, Signature signature, X509Chain chain)
        {
            Trust = trust;
            Signature = signature ?? throw new ArgumentNullException(nameof(signature));
            Chain = chain ?? throw new ArgumentNullException(nameof(chain));
        }
    }
}
