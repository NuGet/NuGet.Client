// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Represents the trust result of a signature.
    /// </summary>
    /// <remarks>The order of the elements on this enum is important.
    /// It should be ordered from most severe to valid.
    /// When a verification with multiple steps wants to be strict it should take the min
    /// out of each step as the status for the whole verification.</remarks>
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
        /// Signature certificate does not match known allowlists.
        /// </summary>
        Untrusted = 3,

        /// <summary>
        /// Signature is valid for the verification step.
        /// </summary>
        Valid = 4
    }
}
