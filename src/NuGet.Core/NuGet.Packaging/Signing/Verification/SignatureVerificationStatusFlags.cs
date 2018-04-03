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
        /// Chain building issues that are found because signature is not conformant with the NuGet Package Signature Spec. This includes:
        ///     CtlNotSignatureValid
        ///     CtlNotValidForUsage
        ///     Cyclic
        ///     ExplicitDistrust
        ///     HasExcludedNameConstraint
        ///     HasNotDefinedNameConstraint
        ///     HasNotPermittedNameConstraint
        ///     HasNotSupportedCriticalExtension
        ///     HasNotSupportedNameConstraint
        ///     HasWeakSignature
        ///     InvalidBasicConstraints
        ///     InvalidExtension
        ///     InvalidNameConstraints
        ///     InvalidPolicyConstraints
        ///     NoIssuanceChainPolicy
        ///     NotSignatureValid
        ///     NotValidForUsage
        ///     PartialChain
        /// </summary>
        ChainBuildingNotConformantWithSpec,

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