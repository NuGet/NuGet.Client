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
        NoErrors                            = 0,

        /// <summary>
        /// A siganture was not found
        /// </summary>
        NoSignature                         = 1,

        /// <summary>
        /// Signer certificate was not found
        /// </summary>
        NoCertificate                       = 2,

        /// <summary>
        /// Multiple signatures where found
        /// </summary>
        MultupleSignatures                  = 4,

        /// <summary>
        /// A call to SignedCms.CheckSignature failed
        /// </summary>
        SignatureCheckFailed                = 8,

        /// <summary>
        /// Signature Algorithm is not supported
        /// </summary>
        SignatureAlgorithmUnsupported       = 16,

        /// <summary>
        /// Public key does not conform with the requirements of the spec
        /// </summary>
        CertificatePublicKeyInvalid         = 32,

        /// <summary>
        /// Signing certificate has lifetimeSigningEku
        /// </summary>
        HasLifetimeSigningEku               = 64,

        /// <summary>
        /// Signer certificate's validity is in the future
        /// </summary>
        CertificateValidityInTheFuture      = 128,

        /// <summary>
        /// Signing certificate has expired
        /// </summary>
        CertificateExpired                  = 256,

        /// <summary>
        /// Hashing algorithm is not supported
        /// </summary>
        HashAlgorithmUnsupported            = 512,

        /// <summary>
        /// Message imprint uses a hash algorithm that is not supported
        /// </summary>
        MessageImprintUnsupportedAlgorithm  = 1024,

        /// <summary>
        /// Integrity check of the signature failed
        /// </summary>
        IntegrityCheckFailed                = 2048,

        /// <summary>
        /// Chain building failures.
        /// Some specific chain building failures (like revocation, revocation status unavailable, certificate expired, etc.)
        /// are not covered by this flag because they are covered specially by another status flag.
        /// </summary>
        ChainBuildingFailure                = 4096,

        /// <summary>
        /// Revocation information was unavailable or was offline for the signer certificate
        /// </summary>
        UnknownRevocation                   = 8192,

        /// <summary>
        /// Signing certificate was revoked
        /// </summary>
        CertificateRevoked                  = 16384,

        /// <summary>
        /// Signing certificate chains to a certificate untrusted by the computer performing the verification
        /// </summary>
        UntrustedRoot                       = 32768,

        /// <summary>
        /// The Timestamp's generalized time was outside certificate's validity period
        /// </summary>
        GeneralizedTimeOutsideValidity      = 65536
    }
}