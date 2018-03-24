// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Packaging.Signing
{
    [Flags]
    public enum SignatureVerificationStatusFlags
    {
        /// <summary>
        /// There was no error found
        /// </summary>
        NoErrors = 0,

        /// <summary>
        /// Signer certificate was not found
        /// </summary>
        NoCertificate,

        /// <summary>
        /// A call to SignedCms.CheckSignature failed
        /// </summary>
        SignatureCheckFailed,

        /// <summary>
        /// Signature Algorithm is not supported
        /// </summary>
        SignatureAlgorithmUnsupported,

        /// <summary>
        /// Public key does not conform with the requirements of the spec
        /// </summary>
        CertificatePublicKeyInvalid,

        /// <summary>
        /// Signing certificate has lifetimeSigningEku
        /// </summary>
        HasLifetimeSigningEku,

        /// <summary>
        /// Signer certificate's validity is in the future
        /// </summary>
        CertificateValidityInTheFuture,

        /// <summary>
        /// Signing certificate has expired
        /// </summary>
        CertificateExpired,

        /// <summary>
        /// Any chain building issue that is not specified falls in this category. This includes:
        ///     NotSignatureValid
        ///     NotValidForUsage
        ///     UntrustedRoot
        ///     Cyclic
        ///     InvalidExtension
        ///     InvalidPolicyConstraints
        ///     InvalidBasicConstraints
        ///     InvalidNameConstraints
        ///     HasNotSupportedNameConstraint
        ///     HasNotDefinedNameConstraint
        ///     HasNotPermittedNameConstraint
        ///     HasExcludedNameConstraint
        ///     PartialChain
        ///     CtlNotSignatureValid
        ///     CtlNotValidForUsage
        ///     NoIssuanceChainPolicy
        /// </summary>
        GeneralChainBuildingIssues,
        // TODO: Should we split this errors to only have soft-illegal chain building issues?
        // Are there any hard-illegal issues in this list currently?

        /// <summary>
        /// Signing certificate was revoked
        /// </summary>
        CertificateRevoked,

        /// <summary>
        /// Signing certificate 
        /// </summary>
        UntrustedRoot,

        /// <summary>
        /// Revocation information was unavailable or was offline for the signer certificate
        /// </summary>
        UnknownRevocation,

        /// <summary>
        /// The signature timestamp was not conforming to the policy or was not valid
        /// </summary>
        /// <remarks>This wrror is because the settings are strict enough to not permit a timestamp error that was present</remarks>
        InvalidTimestamp
    }
}