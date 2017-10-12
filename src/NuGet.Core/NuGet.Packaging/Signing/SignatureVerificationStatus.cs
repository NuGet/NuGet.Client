// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Represents the trust result of a signature.
    /// </summary>
    public enum SignatureVerificationStatus
    {
        /// <summary>
        /// Default unknown value.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Invalid signature.
        /// </summary>
        /// <remarks>This could happen for many reasons such as a tampered with package, invalid hash algorithm, or invalid signing data.</remarks>
        Invalid = 1,

        /// <summary>
        /// Signature is NOT trusted.
        /// </summary>
        Untrusted = 2,

        /// <summary>
        /// Signature is trusted.
        /// </summary>
        Trusted = 3
    }
}
