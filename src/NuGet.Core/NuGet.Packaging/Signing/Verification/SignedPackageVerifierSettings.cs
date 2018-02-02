// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Feed settings used to verify packages.
    /// </summary>
    public sealed class SignedPackageVerifierSettings
    {
        /// <summary>
        /// Allow packages that do not contain signatures.
        /// </summary>
        public bool AllowUnsigned { get; }

        /// <summary>
        /// Allow packages that are not trusted.
        /// </summary>
        public bool AllowUntrusted { get; }

        public bool AllowUntrustedSelfSignedCertificate { get; }

        public bool AllowIgnoreTimestamp { get; }

        public bool AllowMultipleTimestamps { get; }

        public bool AllowNoTimestamp { get; }

        /// <summary>
        /// Treat unknown revocation status as a warning instead of an error during verification.
        /// </summary>
        public bool AllowUnknownRevocation { get; }

        public SignedPackageVerifierSettings(
            bool allowUnsigned,
            bool allowUntrusted,
            bool allowUntrustedSelfSignedCertificate,
            bool allowIgnoreTimestamp,
            bool allowMultipleTimestamps,
            bool allowNoTimestamp,
            bool allowUnknownRevocation)
        {
            AllowUnsigned = allowUnsigned;
            AllowUntrusted = allowUntrusted;
            AllowUntrustedSelfSignedCertificate = allowUntrustedSelfSignedCertificate;
            AllowIgnoreTimestamp = allowIgnoreTimestamp;
            AllowMultipleTimestamps = allowMultipleTimestamps;
            AllowNoTimestamp = allowNoTimestamp;
            AllowUnknownRevocation = allowUnknownRevocation;
        }

        /// <summary>
        /// Allow unsigned.
        /// </summary>
        public static SignedPackageVerifierSettings AllowAll { get; } = new SignedPackageVerifierSettings(
            allowUnsigned: true,
            allowUntrusted: true,
            allowUntrustedSelfSignedCertificate: true,
            allowIgnoreTimestamp: true,
            allowMultipleTimestamps: true,
            allowNoTimestamp: true,
            allowUnknownRevocation: true);

        /// <summary>
        /// Default settings.
        /// </summary>
        public static SignedPackageVerifierSettings Default { get; } = AllowAll;

        /// <summary>
        /// Default policy for scenarios in VS
        /// </summary>
        public static SignedPackageVerifierSettings VSClientDefaultPolicy { get; } = new SignedPackageVerifierSettings(
            allowUnsigned: true,
            allowUntrusted: true,
            allowUntrustedSelfSignedCertificate: true,
            allowIgnoreTimestamp: true,
            allowMultipleTimestamps: true,
            allowNoTimestamp: true,
            allowUnknownRevocation: true);

        /// <summary>
        /// Default policy for nuget.exe verify --signatures command
        /// </summary>
        public static SignedPackageVerifierSettings VerifyCommandDefaultPolicy { get; } = new SignedPackageVerifierSettings(
            allowUnsigned: false,
            allowUntrusted: false,
            allowUntrustedSelfSignedCertificate: true,
            allowIgnoreTimestamp: false,
            allowMultipleTimestamps: true,
            allowNoTimestamp: true,
            allowUnknownRevocation: true);
    }
}