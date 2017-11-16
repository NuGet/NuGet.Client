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

namespace NuGet.Commands
{
    /// <summary>
    /// Command Runner used to run the business logic for nuget verify command
    /// </summary>
    public class VerifyCommandRunner : IVerifyCommandRunner
    {
        public async Task<int> ExecuteCommandAsync(VerifyArgs verifyArgs)
        {
            if (verifyArgs.Type != VerifyArgs.VerificationType.Signatures)
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.VerifyCommand_VerificationTypeNotSupported));
            }

            var packagesToVerify = LocalFolderUtility.ResolvePackageFromPath(verifyArgs.PackagePath);
            LocalFolderUtility.EnsurePackageFileExists(verifyArgs.PackagePath, packagesToVerify);

            var trustProviders = SignatureVerificationProviderFactory.GetSignatureVerificationProviders();
            var verifier = new PackageSignatureVerifier(trustProviders, SignedPackageVerifierSettings.RequireSigned);

            var errorCount = 0;

            foreach (var package in packagesToVerify)
            {
                try
                {
                    errorCount += await VerifyPackageAsync(package, verifyArgs.Logger, verifier);
                }
                catch (InvalidDataException)
                {
                    verifyArgs.Logger.LogError(string.Format(CultureInfo.CurrentCulture, Strings.VerifyCommand_PackageIsNotValid, package));
                }
            }

            return (errorCount > 0) ? 1 : 0;
        }

        private async Task<int> VerifyPackageAsync(string packagePath, ILogger logger, PackageSignatureVerifier verifier)
        {
            var result = 0;
            using (var packageReader = new PackageArchiveReader(packagePath))
            {
                var verificationResult = await verifier.VerifySignaturesAsync(packageReader, logger, CancellationToken.None);

                if (verificationResult.Valid)
                {
                    logger.LogInformation("Successfully verified package integrity and author signature.");
                }
                else
                {
                    var logMessages = verificationResult.Results.SelectMany(p => p.Issues).Select(p => p.ToLogMessage()).ToList();
                    await logger.LogMessagesAsync(logMessages);
                    if (logMessages.Any(m => m.Level >= LogLevel.Warning))
                    {
                        var errors = logMessages.Where(m => m.Level == LogLevel.Error).Count();
                        var warnings = logMessages.Where(m => m.Level == LogLevel.Warning).Count();

                        logger.LogInformation($"Finished with {errors} errors and {warnings} warnings.");

                        result = errors;
                    }
                }

                return result;
            }
        }
    }
}
