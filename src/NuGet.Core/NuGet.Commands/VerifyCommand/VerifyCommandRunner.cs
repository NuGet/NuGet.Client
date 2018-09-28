// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Signing;
using NuGet.Protocol;
using static NuGet.Commands.VerifyArgs;

namespace NuGet.Commands
{
    /// <summary>
    /// Command Runner used to run the business logic for nuget verify command
    /// </summary>
    public class VerifyCommandRunner : IVerifyCommandRunner
    {
        private const int SuccessCode = 0;
        private const int FailureCode = 1;
        private const HashAlgorithmName _defaultFingerprintAlgorithm = HashAlgorithmName.SHA256;
        private const bool RequireAllowList = false;

        public async Task<int> ExecuteCommandAsync(VerifyArgs verifyArgs)
        {
            if (verifyArgs.Verifications.Count == 0)
            {
                verifyArgs.Logger.LogError(string.Format(CultureInfo.CurrentCulture, Strings.VerifyCommand_VerificationTypeNotSupported));
                return FailureCode;
            }

            var errorCount = 0;

            if (ShouldExecuteVerification(verifyArgs, Verification.Signatures))
            {
                var packagesToVerify = LocalFolderUtility.ResolvePackageFromPath(verifyArgs.PackagePath);
                LocalFolderUtility.EnsurePackageFileExists(verifyArgs.PackagePath, packagesToVerify);

                var allowListEntries = verifyArgs.CertificateFingerprint.Select(fingerprint =>
                    new CertificateHashAllowListEntry(
                        VerificationTarget.Author | VerificationTarget.Repository,
                        SignaturePlacement.PrimarySignature,
                        fingerprint,
                        _defaultFingerprintAlgorithm)).ToList();

                var verifierSettings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy();
                var verificationProviders = SignatureVerificationProviderFactory.GetDefaultSignatureVerificationProviders();

                if (allowListEntries != null && !allowListEntries.Any())
                {
                    verificationProviders.Add(
                        new AllowListVerificationProvider(
                            allowListEntries,
                            RequireAllowList,
                            emptyListErrorMessage: Strings.Error_NoProvidedAllowList,
                            noMatchErrorMessage: Strings.Error_NoMatchingCertificate));
                }

                var verifier = new PackageSignatureVerifier(verificationProviders);

                foreach (var package in packagesToVerify)
                {
                    try
                    {
                        errorCount += await VerifySignatureForPackageAsync(package, verifyArgs.Logger, verifier, verifierSettings);
                    }
                    catch (InvalidDataException e)
                    {
                        verifyArgs.Logger.LogError(string.Format(CultureInfo.CurrentCulture, Strings.VerifyCommand_PackageIsNotValid, package));
                        ExceptionUtilities.LogException(e, verifyArgs.Logger);
                    }
                }
            }

            return errorCount == 0 ? SuccessCode : FailureCode;
        }

        private async Task<int> VerifySignatureForPackageAsync(string packagePath, ILogger logger, PackageSignatureVerifier verifier, SignedPackageVerifierSettings verifierSettings)
        {
            var result = 0;
            using (var packageReader = new PackageArchiveReader(packagePath))
            {
                var verificationResult = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);

                var packageIdentity = packageReader.GetIdentity();

                logger.LogInformation(Environment.NewLine + string.Format(CultureInfo.CurrentCulture,
                    Strings.VerifyCommand_VerifyingPackage,
                    packageIdentity.ToString()));
                logger.LogInformation($"{packagePath}{Environment.NewLine}");

                var logMessages = verificationResult.Results.SelectMany(p => p.Issues).ToList();
                await logger.LogMessagesAsync(logMessages);

                if (logMessages.Any(m => m.Level >= LogLevel.Warning))
                {
                    var errors = logMessages.Count(m => m.Level == LogLevel.Error);
                    var warnings = logMessages.Count(m => m.Level == LogLevel.Warning);

                    logger.LogInformation(string.Format(CultureInfo.CurrentCulture, Strings.VerifyCommand_FinishedWithErrors, errors, warnings));

                    result = errors;
                }

                if (verificationResult.Valid)
                {
                    logger.LogInformation(Environment.NewLine + string.Format(CultureInfo.CurrentCulture, Strings.VerifyCommand_Success, packageIdentity.ToString()));
                }
                else
                {
                    logger.LogError(Environment.NewLine + Strings.VerifyCommand_Failed);
                }

                return result;
            }
        }

        private bool ShouldExecuteVerification(VerifyArgs args, Verification v)
        {
            return args.Verifications.Any(verification => verification == Verification.All || verification == v);
        }
    }
}
