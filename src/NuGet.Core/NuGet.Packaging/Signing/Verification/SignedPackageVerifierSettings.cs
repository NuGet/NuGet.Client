// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using NuGet.Common;
using NuGet.Configuration;

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

        /// <summary>
        /// Allow ignoring timestamp.
        /// </summary>
        public bool AllowIgnoreTimestamp { get; }

        /// <summary>
        /// Allow more than one timestamp.
        /// </summary>
        public bool AllowMultipleTimestamps { get; }

        /// <summary>
        /// Allow no timestamp.
        /// </summary>
        public bool AllowNoTimestamp { get; }

        /// <summary>
        /// Treat unknown revocation status as a warning instead of an error during verification.
        /// </summary>
        public bool AllowUnknownRevocation { get; }

        /// <summary>
        /// Report unknown revocation status.
        /// </summary>
        public bool ReportUnknownRevocation { get; }

        /// <summary>
        /// Allow an empty or null RepositoryCertificateList.
        /// </summary>
        public bool AllowNoRepositoryCertificateList { get; }

        /// <summary>
        /// Allow an empty or null ClientCertificateList.
        /// </summary>
        public bool AllowNoClientCertificateList { get; }

        /// <summary>
        /// Allow an empty or null AllowList
        /// </summary>
        public bool AllowEmptyAllowList { get; }

        /// <summary>
        /// Gets the verification target(s).
        /// </summary>
        public VerificationTarget VerificationTarget { get; }

        /// <summary>
        /// Gets the placement of verification target(s).
        /// </summary>
        public SignaturePlacement SignaturePlacement { get; }

        /// <summary>
        /// Gets the repository countersignature verification behavior.
        /// </summary>
        public SignatureVerificationBehavior RepositoryCountersignatureVerificationBehavior { get; }

        /// <summary>
        /// Gets how the revocation verification should be performed.
        /// </summary>
        public RevocationMode RevocationMode { get; }

        /// <summary>
        /// Allowlist of repository certificates hashes.
        /// </summary>
        public IReadOnlyList<VerificationAllowListEntry> RepositoryCertificateList { get; }

        /// <summary>
        /// Allowlist of client side certificate hashes.
        /// </summary>
        public IReadOnlyList<VerificationAllowListEntry> ClientCertificateList { get; }

        /// <summary>
        /// List of signatures allowed in verification.
        /// </summary>
        public IReadOnlyCollection<VerificationAllowListEntry> AllowList { get; }

        public SignedPackageVerifierSettings(
            bool allowUnsigned,
            bool allowIllegal,
            bool allowUntrusted,
            bool allowIgnoreTimestamp,
            bool allowMultipleTimestamps,
            bool allowNoTimestamp,
            bool allowUnknownRevocation,
            bool reportUnknownRevocation,
            bool allowNoRepositoryCertificateList,
            bool allowNoClientCertificateList,
            VerificationTarget verificationTarget,
            SignaturePlacement signaturePlacement,
            SignatureVerificationBehavior repositoryCountersignatureVerificationBehavior,
            RevocationMode revocationMode)
            : this(
                  allowUnsigned,
                  allowIllegal,
                  allowUntrusted,
                  allowIgnoreTimestamp,
                  allowMultipleTimestamps,
                  allowNoTimestamp,
                  allowUnknownRevocation,
                  reportUnknownRevocation,
                  allowNoRepositoryCertificateList,
                  allowNoClientCertificateList,
                  verificationTarget,
                  signaturePlacement,
                  repositoryCountersignatureVerificationBehavior,
                  revocationMode,
                  allowListEntries: null,
                  clientAllowListEntries: null)
        {
        }

        public SignedPackageVerifierSettings(
            bool allowUnsigned,
            bool allowIllegal,
            bool allowUntrusted,
            bool allowIgnoreTimestamp,
            bool allowMultipleTimestamps,
            bool allowNoTimestamp,
            bool allowUnknownRevocation,
            bool reportUnknownRevocation,
            bool allowNoRepositoryCertificateList,
            bool allowNoClientCertificateList,
            VerificationTarget verificationTarget,
            SignaturePlacement signaturePlacement,
            SignatureVerificationBehavior repositoryCountersignatureVerificationBehavior,
            RevocationMode revocationMode,
            IReadOnlyCollection<VerificationAllowListEntry> allowListEntries,
            IReadOnlyList<VerificationAllowListEntry> clientAllowListEntries)
        {
            if (!Enum.IsDefined(typeof(VerificationTarget), verificationTarget))
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.UnrecognizedEnumValue,
                        verificationTarget),
                    nameof(verificationTarget));
            }

            if (!Enum.IsDefined(typeof(SignaturePlacement), signaturePlacement))
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.UnrecognizedEnumValue,
                        signaturePlacement),
                    nameof(signaturePlacement));
            }

            if (!Enum.IsDefined(typeof(SignatureVerificationBehavior), repositoryCountersignatureVerificationBehavior))
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.UnrecognizedEnumValue,
                        repositoryCountersignatureVerificationBehavior),
                    nameof(repositoryCountersignatureVerificationBehavior));
            }

            if ((signaturePlacement.HasFlag(SignaturePlacement.Countersignature) && !verificationTarget.HasFlag(VerificationTarget.Repository)) ||
                (signaturePlacement == SignaturePlacement.Countersignature && verificationTarget != VerificationTarget.Repository))
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.InvalidArgumentCombination,
                        nameof(verificationTarget),
                        nameof(signaturePlacement)),
                    nameof(signaturePlacement));
            }

            if ((repositoryCountersignatureVerificationBehavior == SignatureVerificationBehavior.Never) ==
                    signaturePlacement.HasFlag(SignaturePlacement.Countersignature) ||
                ((repositoryCountersignatureVerificationBehavior == SignatureVerificationBehavior.Always) &&
                    !signaturePlacement.HasFlag(SignaturePlacement.Countersignature)))
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.InvalidArgumentCombination,
                        nameof(signaturePlacement),
                        nameof(repositoryCountersignatureVerificationBehavior)),
                    nameof(repositoryCountersignatureVerificationBehavior));
            }

            AllowUnsigned = allowUnsigned;
            AllowIllegal = allowIllegal;
            AllowUntrusted = allowUntrusted;
            AllowIgnoreTimestamp = allowIgnoreTimestamp;
            AllowMultipleTimestamps = allowMultipleTimestamps;
            AllowNoTimestamp = allowNoTimestamp;
            AllowUnknownRevocation = allowUnknownRevocation;
            ReportUnknownRevocation = reportUnknownRevocation;
            AllowNoRepositoryCertificateList = allowNoRepositoryCertificateList;
            AllowNoClientCertificateList = allowNoClientCertificateList;
            VerificationTarget = verificationTarget;
            SignaturePlacement = signaturePlacement;
            RepositoryCountersignatureVerificationBehavior = repositoryCountersignatureVerificationBehavior;
            RevocationMode = revocationMode;
            AllowList = allowListEntries;
            ClientCertificateList = clientAllowListEntries;
        }

        /// <summary>
        /// Gives the appropriate configuration depending on the user specified settings.
        /// </summary>
        /// <param name="settings">Loaded settings used to get user data</param>
        public static SignedPackageVerifierSettings GetClientPolicy(ISettings settings, ILogger logger)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var policy = SettingsUtility.GetSignatureValidationMode(settings);

            var allowList = TrustedSignersProvider.GetAllowListEntries(settings, logger);

            if (policy == SignatureValidationMode.Require)
            {
                return GetRequireModeDefaultPolicy(clientAllowListEntries: allowList);
            }

            return GetAcceptModeDefaultPolicy(clientAllowListEntries: allowList);
        }

        /// <summary>
        /// Default settings.
        /// </summary>
        public static SignedPackageVerifierSettings GetDefault(
            IReadOnlyCollection<VerificationAllowListEntry> allowListEntries = null,
            IReadOnlyList<VerificationAllowListEntry> clientAllowListEntries = null)
        {
            return new SignedPackageVerifierSettings(
                allowUnsigned: true,
                allowIllegal: true,
                allowUntrusted: true,
                allowIgnoreTimestamp: true,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                reportUnknownRevocation: false,
                allowNoRepositoryCertificateList: true,
                allowNoClientCertificateList: true,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExistsAndIsNecessary,
                revocationMode: SettingsUtility.GetRevocationMode(),
                allowListEntries: allowListEntries,
                clientAllowListEntries: clientAllowListEntries);
        }

        /// <summary>
        /// The accept mode policy.
        /// </summary>
        public static SignedPackageVerifierSettings GetAcceptModeDefaultPolicy(
            IReadOnlyCollection<VerificationAllowListEntry> allowListEntries = null,
            IReadOnlyList<VerificationAllowListEntry> clientAllowListEntries = null)
        {
            return new SignedPackageVerifierSettings(
                allowUnsigned: true,
                allowIllegal: true,
                allowUntrusted: true,
                allowIgnoreTimestamp: true,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                reportUnknownRevocation: false,
                allowNoRepositoryCertificateList: true,
                allowNoClientCertificateList: true,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExistsAndIsNecessary,
                revocationMode: SettingsUtility.GetRevocationMode(),
                allowListEntries: allowListEntries,
                clientAllowListEntries: clientAllowListEntries);
        }

        /// <summary>
        /// The require mode policy.
        /// </summary>
        public static SignedPackageVerifierSettings GetRequireModeDefaultPolicy(
            IReadOnlyCollection<VerificationAllowListEntry> allowListEntries = null,
            IReadOnlyList<VerificationAllowListEntry> clientAllowListEntries = null)
        {
            return new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: true,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                allowNoRepositoryCertificateList: true,
                allowNoClientCertificateList: false,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExistsAndIsNecessary,
                revocationMode: SettingsUtility.GetRevocationMode(),
                allowListEntries: allowListEntries,
                clientAllowListEntries: clientAllowListEntries);
        }

        /// <summary>
        /// Default policy for nuget.exe verify --signatures command.
        /// </summary>
        public static SignedPackageVerifierSettings GetVerifyCommandDefaultPolicy(
            IReadOnlyCollection<VerificationAllowListEntry> allowListEntries = null,
            IReadOnlyList<VerificationAllowListEntry> clientAllowListEntries = null)
        {
            return new SignedPackageVerifierSettings(
                allowUnsigned: false,
                allowIllegal: false,
                allowUntrusted: false,
                allowIgnoreTimestamp: false,
                allowMultipleTimestamps: true,
                allowNoTimestamp: true,
                allowUnknownRevocation: true,
                reportUnknownRevocation: true,
                allowNoRepositoryCertificateList: true,
                allowNoClientCertificateList: true,
                verificationTarget: VerificationTarget.All,
                signaturePlacement: SignaturePlacement.Any,
                repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExists,
                revocationMode: SettingsUtility.GetRevocationMode(),
                allowListEntries: allowListEntries,
                clientAllowListEntries: clientAllowListEntries);
        }
    }
}