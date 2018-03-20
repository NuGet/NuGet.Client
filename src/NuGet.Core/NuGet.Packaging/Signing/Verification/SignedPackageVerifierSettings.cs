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
        /// Allow packages with signatures that do not conform to the specification.
        /// </summary>
        public bool AllowIllegal { get; }

        /// <summary>
        /// Allow packages that have not been explicitly trusted by the consumer.
        /// </summary>
        public bool AllowUntrusted { get; }

        public bool AllowUntrustedSelfIssuedCertificate { get; }

        public bool AllowIgnoreTimestamp { get; }

        public bool AllowMultipleTimestamps { get; }

        public bool AllowNoTimestamp { get; }

        /// <summary>
        /// Treat unknown revocation status as a warning instead of an error during verification.
        /// </summary>
        public bool AllowUnknownRevocation { get; }

        public SignedPackageVerifierSettings(
            bool allowUnsigned,
            bool allowIllegal,
            bool allowUntrusted,
            bool allowUntrustedSelfIssuedCertificate,
            bool allowIgnoreTimestamp,
            bool allowMultipleTimestamps,
            bool allowNoTimestamp,
            bool allowUnknownRevocation)
        {
            AllowUnsigned = allowUnsigned;
            AllowIllegal = allowIllegal;
            AllowUntrusted = allowUntrusted;
            AllowUntrustedSelfIssuedCertificate = allowUntrustedSelfIssuedCertificate;
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
            allowIllegal: true,
            allowUntrusted: true,
            allowUntrustedSelfIssuedCertificate: true,
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
            allowIllegal: true,
            allowUntrusted: true,
            allowUntrustedSelfIssuedCertificate: true,
            allowIgnoreTimestamp: true,
            allowMultipleTimestamps: true,
            allowNoTimestamp: true,
            allowUnknownRevocation: true);

        /// <summary>
        /// Default policy for nuget.exe verify --signatures command
        /// </summary>
        public static SignedPackageVerifierSettings VerifyCommandDefaultPolicy { get; } = new SignedPackageVerifierSettings(
            allowUnsigned: false,
            allowIllegal: false,
            allowUntrusted: false,
            allowUntrustedSelfIssuedCertificate: true,
            allowIgnoreTimestamp: false,
            allowMultipleTimestamps: true,
            allowNoTimestamp: true,
            allowUnknownRevocation: true);
    }
}