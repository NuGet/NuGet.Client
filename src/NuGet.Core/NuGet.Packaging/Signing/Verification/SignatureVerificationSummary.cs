// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Packaging.Signing
{
    public class SignatureVerificationSummary
    {
        /// <summary>
        /// Type of the signature that was verified
        /// </summary>
        public SignatureType SignatureType { get; }

        /// <summary>
        /// Status of the verification
        /// </summary>
        public SignatureVerificationStatus Status { get; }

        /// <summary>
        /// Reasons for the status.
        /// </summary>
        public SignatureVerificationStatusFlags Flags { get; }

        /// <summary>
        /// Timestamp used to validate certificate.
        /// </summary>
        public Timestamp Timestamp { get; }

        /// <summary>
        /// Expiration Date and Time for signature
        /// </summary>
        /// <remarks>This field will only be set if the flag CertificateExpired is present Flags</remarks>
        public DateTime? ExpirationTime { get; }

        public SignatureVerificationSummary(
            SignatureType signatureType,
            SignatureVerificationStatus status,
            SignatureVerificationStatusFlags flags,
            Timestamp timestamp,
            DateTime? expirationTime)
        {
            SignatureType = signatureType;
            Status = status;
            Flags = flags;
            Timestamp = timestamp;
            ExpirationTime = expirationTime;
        }

        public SignatureVerificationSummary(
            SignatureType signatureType,
            SignatureVerificationStatus status,
            SignatureVerificationStatusFlags flags,
            Timestamp timestamp)
            : this(signatureType, status, flags, timestamp, expirationTime: null)
        {
        }

        public SignatureVerificationSummary(
            SignatureType signatureType,
            SignatureVerificationStatus status,
            SignatureVerificationStatusFlags flags)
            : this(signatureType, status, flags, timestamp: null, expirationTime: null)
        {
        }
    }
}
