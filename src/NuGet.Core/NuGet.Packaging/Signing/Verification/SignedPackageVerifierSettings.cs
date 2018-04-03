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

        public bool AllowIgnoreTimestamp { get; }

        public bool AllowMultipleTimestamps { get; }

        public bool AllowNoTimestamp { get; }

        public bool AllowNoTrustedAuthors { get; }

        public bool AllowNoTrustedSources { get; }

        public bool AllowAlwaysVerifyingCountersignature { get; }

        /// <summary>
        /// Treat unknown revocation status as a warning instead of an error during verification.
        /// </summary>
        public bool AllowUnknownRevocation { get; }

        public SignedPackageVerifierSettings(
            bool allowUnsigned,
            bool allowIllegal,
            bool allowUntrusted,
            bool allowIgnoreTimestamp,
            bool allowMultipleTimestamps,
            bool allowNoTimestamp,
            bool allowUnknownRevocation,
            bool allowNoTrustedAuthors,
            bool allowNoTrustedSources,
            bool allowAlwaysVerifyingCountersignature)
        {
            AllowUnsigned = allowUnsigned;
            AllowIllegal = allowIllegal;
            AllowUntrusted = allowUntrusted;
            AllowIgnoreTimestamp = allowIgnoreTimestamp;
            AllowMultipleTimestamps = allowMultipleTimestamps;
            AllowNoTimestamp = allowNoTimestamp;
            AllowUnknownRevocation = allowUnknownRevocation;
            AllowNoTrustedAuthors = allowNoTrustedAuthors;
            AllowNoTrustedSources = allowNoTrustedSources;
            AllowAlwaysVerifyingCountersignature = allowAlwaysVerifyingCountersignature;
        }

        /// <summary>
        /// Allow unsigned.
        /// </summary>
        public static SignedPackageVerifierSettings AllowAll { get; } = new SignedPackageVerifierSettings(
            allowUnsigned: true,
            allowIllegal: true,
            allowUntrusted: true,
            allowIgnoreTimestamp: true,
            allowMultipleTimestamps: true,
            allowNoTimestamp: true,
            allowUnknownRevocation: true,
            allowNoTrustedAuthors: true,
            allowNoTrustedSources: true,
            allowAlwaysVerifyingCountersignature: true);

        /// <summary>
        /// Default settings.
        /// </summary>
        public static SignedPackageVerifierSettings Default { get; } = AllowAll;

        /// <summary>
        /// Default policy for scenarios in Accept mode
        /// </summary>
        public static SignedPackageVerifierSettings AcceptModeDefaultPolicy { get; } = new SignedPackageVerifierSettings(
            allowUnsigned: true,
            allowIllegal: true,
            allowUntrusted: true,
            allowIgnoreTimestamp: true,
            allowMultipleTimestamps: true,
            allowNoTimestamp: true,
            allowUnknownRevocation: true,
            allowNoTrustedAuthors: true,
            allowNoTrustedSources: true,
            allowAlwaysVerifyingCountersignature: false);

        /// <summary>
        /// Default policy for scenarios in Require mode
        /// </summary>
        public static SignedPackageVerifierSettings RequireModeDefaultPolicy { get; } = new SignedPackageVerifierSettings(
            allowUnsigned: false,
            allowIllegal: false,
            allowUntrusted: false,
            allowIgnoreTimestamp: true,
            allowMultipleTimestamps: true,
            allowNoTimestamp: true,
            allowUnknownRevocation: true,
            allowNoTrustedAuthors: false,
            allowNoTrustedSources: false,
            allowAlwaysVerifyingCountersignature: false);

        /// <summary>
        /// Default policy for nuget.exe verify --signatures command
        /// </summary>
        public static SignedPackageVerifierSettings VerifyCommandDefaultPolicy { get; } = new SignedPackageVerifierSettings(
            allowUnsigned: false,
            allowIllegal: false,
            allowUntrusted: false,
            allowIgnoreTimestamp: false,
            allowMultipleTimestamps: true,
            allowNoTimestamp: true,
            allowUnknownRevocation: true,
            allowNoTrustedAuthors: true,
            allowNoTrustedSources: true,
            allowAlwaysVerifyingCountersignature: true);
    }
}