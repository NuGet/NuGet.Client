// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Request for timestamping a signature
    /// </summary>
    public class TimestampRequest
    {
        /// <summary>
        /// Signing Specification for this timestamp request.
        /// </summary>
        public SigningSpecifications SigningSpec { get; }

        /// <summary>
        /// Hash of Signature that needs to be timestamped.
        /// </summary>
        public byte[] SignatureMessageHash { get; }

        /// <summary>
        /// Hash algorithm to be used for timestamping.
        /// </summary>
        public HashAlgorithmName TimestampHashAlgorithm { get; }

        /// <summary>
        /// Placement of signature to be timestamped.
        /// </summary>
        public SignaturePlacement TimestampSignaturePlacement { get; }

        public TimestampRequest(SigningSpecifications signingSpec, byte[] signatureMessageHash, HashAlgorithmName timestampHashAlgorithm, SignaturePlacement timestampSignaturePlacement)
        {
            SigningSpec = signingSpec ?? throw new ArgumentNullException(nameof(signingSpec));
            SignatureMessageHash = signatureMessageHash ?? throw new ArgumentNullException(nameof(signatureMessageHash));
            TimestampHashAlgorithm = timestampHashAlgorithm;
            TimestampSignaturePlacement = timestampSignaturePlacement;
        }
    }
}
