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
        public async Task<int> ExecuteCommandAsync(VerifyArgs verifyArgs)
        {
            var executedSuccessfully = false;
            
            if (ShouldExecuteType(verifyArgs, VerificationType.Signatures))
            {
                var packagesToVerify = LocalFolderUtility.ResolvePackageFromPath(verifyArgs.PackagePath);
                LocalFolderUtility.EnsurePackageFileExists(verifyArgs.PackagePath, packagesToVerify);

                var trustProviders = SignatureVerificationProviderFactory.GetSignatureVerificationProviders();
                var verifier = new PackageSignatureVerifier(trustProviders, SignedPackageVerifierSettings.RequireSigned);

                var errorCount = 0;

                foreach (var package in packagesToVerify)
                {
                    try
                    {
                        errorCount += await VerifySignatureForPackageAsync(package, verifyArgs.Logger, verifier);
                    }
                    catch (InvalidDataException e)
                    {
                        verifyArgs.Logger.LogError(string.Format(CultureInfo.CurrentCulture, Strings.VerifyCommand_PackageIsNotValid, package));
                        verifyArgs.Logger.LogDebug(string.Format(CultureInfo.CurrentCulture, Strings.Error_CommandFailedWithException, nameof(VerifySignatureForPackageAsync), e.Message));
                    }
                }
                executedSuccessfully = errorCount == 0;
            }

            if (verifyArgs.Verifications.Count == 0)
            {
                verifyArgs.Logger.LogError(string.Format(CultureInfo.CurrentCulture, Strings.VerifyCommand_VerificationTypeNotSupported));
            }

            return executedSuccessfully ? 0 : 1;
        }

        private async Task<int> VerifySignatureForPackageAsync(string packagePath, ILogger logger, PackageSignatureVerifier verifier)
        {
            var result = 0;
            using (var packageReader = new PackageArchiveReader(packagePath))
            {
                var verificationResult = await verifier.VerifySignaturesAsync(packageReader, CancellationToken.None);

                var packageIdentity = packageReader.GetIdentity();

                logger.LogInformation(Environment.NewLine + string.Format(CultureInfo.CurrentCulture,
                    Strings.VerifyCommand_VerifyingPackage,
                    packageIdentity.ToString()));
                logger.LogInformation($"{packagePath}{Environment.NewLine}");

                var logMessages = verificationResult.Results.SelectMany(p => p.Issues).Select(p => p.ToLogMessage()).ToList();
                await logger.LogMessagesAsync(logMessages);

                if (logMessages.Any(m => m.Level >= LogLevel.Warning))
                {
                    var errors = logMessages.Where(m => m.Level == LogLevel.Error).Count();
                    var warnings = logMessages.Where(m => m.Level == LogLevel.Warning).Count();

                    logger.LogInformation(string.Format(CultureInfo.CurrentCulture, Strings.VerifyCommand_FinishedWithErrors, errors, warnings));

                    result = errors;
                }

                if (verificationResult.Valid)
                {
                    logger.LogInformation(Environment.NewLine + Strings.VerifyCommand_Success);
                }

                return result;
            }
        }

        private bool ShouldExecuteType(VerifyArgs args, VerificationType type)
        {
            return args.Verifications.Count() > 0 && (args.Verifications.First() == VerificationType.All || args.Verifications.Contains(type));
        }
    }
}
