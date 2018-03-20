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
        /// Invalid package signature.
        /// </summary>
        /// <remarks>This could happen because the package integrity check fails or the certificate is revoked.</remarks>
        Suspect = 1,

        /// <summary>
        /// Signature does not conform with NuGet signing specification.
        /// </summary>
        Illegal = 2,


        /// <summary>
        /// Signature is not explicitly trusted by the consumer. 
        /// </summary>
        Untrusted = 3,

        /// <summary>
        /// Signature is valid for the verification step.
        /// </summary>
        Valid = 4
    }
}
