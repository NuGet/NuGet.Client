// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Protocol;

namespace NuGet.Commands
{
    /// <summary>
    /// Command Runner used to run the business logic for nuget sign command
    /// </summary>
    public class SignCommandRunner : ISignCommandRunner
    {
        public async Task<int> ExecuteCommandAsync(SignArgs signArgs)
        {
            // resolve path into multiple packages if needed.
            var packagesToSign = LocalFolderUtility.ResolvePackageFromPath(signArgs.PackagePath);
            LocalFolderUtility.EnsurePackageFileExists(signArgs.PackagePath, packagesToSign);

            var cert = await GetCertificateAsync(signArgs);

            signArgs.Logger.LogInformation(Environment.NewLine);
            signArgs.Logger.LogInformation(Strings.SignCommandDisplayCertificate);
            signArgs.Logger.LogInformation(CertificateUtility.X509Certificate2ToString(cert));

            if (!string.IsNullOrEmpty(signArgs.Timestamper))
            {
                signArgs.Logger.LogInformation(Strings.SignCommandDisplayTimestamper);
                signArgs.Logger.LogInformation(signArgs.Timestamper);
            }

            if (!string.IsNullOrEmpty(signArgs.OutputDirectory))
            {
                signArgs.Logger.LogInformation(Strings.SignCommandOutputPath);
                signArgs.Logger.LogInformation(signArgs.OutputDirectory);
            }

            using (var signRequest = GenerateSignPackageRequest(signArgs, cert))
            {
                return await ExecuteCommandAsync(
                    packagesToSign,
                    signRequest,
                    signArgs.Timestamper,
                    signArgs.Logger,
                    signArgs.OutputDirectory,
                    signArgs.Overwrite,
                    signArgs.Token);
            }
        }

        public async Task<int> ExecuteCommandAsync(
            IEnumerable<string> packagesToSign,
            SignPackageRequest signPackageRequest,
            string timestamper,
            ILogger logger,
            string outputDirectory,
            bool overwrite,
            CancellationToken token)
        {
            var success = true;

            var signatureProvider = GetSignatureProvider(timestamper);

            foreach (var packagePath in packagesToSign)
            {
                try
                {
                    string outputPath;

                    if (string.IsNullOrEmpty(outputDirectory))
                    {
                        outputPath = packagePath;
                    }
                    else
                    {
                        outputPath = Path.Combine(outputDirectory, Path.GetFileName(packagePath));
                    }

                    await SignPackageAsync(packagePath, outputPath, logger, overwrite, signatureProvider, signPackageRequest, token);
                }
                catch (Exception e)
                {
                    success = false;
                    ExceptionUtilities.LogException(e, logger);
                }
            }

            if (success)
            {
                logger.LogInformation(Strings.SignCommandSuccess);
            }

            return success ? 0 : 1;
        }

        private static ISignatureProvider GetSignatureProvider(string timestamper)
        {
            Rfc3161TimestampProvider timestampProvider = null;

            if (!string.IsNullOrEmpty(timestamper))
            {
                timestampProvider = new Rfc3161TimestampProvider(new Uri(timestamper));
            }

            return new X509SignatureProvider(timestampProvider);
        }

        private async Task<int> SignPackageAsync(
            string packagePath,
            string outputPath,
            ILogger logger,
            bool Overwrite,
            ISignatureProvider signatureProvider,
            SignPackageRequest request,
            CancellationToken token)
        {
            var tempFilePath = Path.GetTempFileName();

            using (var packageReadStream = File.Open(packagePath, FileMode.Open))
            using (var packageWriteStream = File.Open(tempFilePath, FileMode.Open))
            {
                if (Overwrite)
                {
                    await RemoveSignatureAsync(logger, signatureProvider, packageReadStream, packageWriteStream, token);
                }

                await AddSignatureAsync(logger, signatureProvider, request, packageReadStream, packageWriteStream, token);
            }

            OverwritePackage(tempFilePath, outputPath);

            FileUtility.Delete(tempFilePath);

            return 0;
        }

        private static async Task AddSignatureAsync(
            ILogger logger,
            ISignatureProvider signatureProvider,
            SignPackageRequest request,
            FileStream packageReadStream,
            FileStream packageWriteStream,
            CancellationToken token)
        {
            using (var package = new SignedPackageArchive(packageReadStream, packageWriteStream))
            {
                var signer = new Signer(package, signatureProvider);
                await signer.SignAsync(request, logger, token);
            }
        }

        private static async Task RemoveSignatureAsync(
            ILogger logger,
            ISignatureProvider signatureProvider,
            FileStream packageReadStream,
            FileStream packageWriteStream,
            CancellationToken token)
        {
            using (var package = new SignedPackageArchive(packageReadStream, packageWriteStream))
            {
                var signer = new Signer(package, signatureProvider);
                await signer.RemoveSignaturesAsync(logger, token);
            }
        }

        private static void OverwritePackage(string sourceFilePath, string destFilePath)
        {
            File.Copy(sourceFilePath, destFilePath, overwrite: true);
        }

        private SignPackageRequest GenerateSignPackageRequest(SignArgs signArgs, X509Certificate2 certificate)
        {
            return new SignPackageRequest
            {
                Certificate = certificate,
                SignatureHashAlgorithm = signArgs.SignatureHashAlgorithm,
                TimestampHashAlgorithm = signArgs.TimestampHashAlgorithm
            };
        }

        private static async Task<X509Certificate2> GetCertificateAsync(SignArgs signArgs)
        {
            var certFindOptions = new CertificateSourceOptions()
            {
                CertificatePath = signArgs.CertificatePath,
                CertificatePassword = signArgs.CertificatePassword,
                Fingerprint = signArgs.CertificateFingerprint,
                StoreLocation = signArgs.CertificateStoreLocation,
                StoreName = signArgs.CertificateStoreName,
                SubjectName = signArgs.CertificateSubjectName,
                NonInteractive = signArgs.NonInteractive,
                PasswordProvider = signArgs.PasswordProvider,
                Token = signArgs.Token
            };

            // get matching certificates
            var matchingCertCollection = await CertificateProvider.GetCertificatesAsync(certFindOptions);

            if (matchingCertCollection.Count > 1)
            {
#if IS_DESKTOP
                if (signArgs.NonInteractive || !RuntimeEnvironmentHelper.IsWindows)
                {
                    // if on non-windows os or in non interactive mode - display the certs and error out
                    signArgs.Logger.LogInformation(CertificateUtility.X509Certificate2CollectionToString(matchingCertCollection));
                    throw new SignCommandException(
                        LogMessage.CreateError(NuGetLogCode.NU3003,
                        string.Format(Strings.SignCommandMultipleCertException,
                        nameof(SignArgs.CertificateFingerprint))));
                }
                else
                {
                    // Else launch UI to select
                    matchingCertCollection = X509Certificate2UI.SelectFromCollection(
                        FilterCodeSigningCertificates(matchingCertCollection),
                        Strings.SignCommandDialogTitle,
                        Strings.SignCommandDialogMessage,
                        X509SelectionFlag.SingleSelection);
                }
#else
                // if on non-windows os or in non interactive mode - display and error out
                signArgs.Logger.LogError(CertificateUtility.X509Certificate2CollectionToString(matchingCertCollection));

                throw new SignCommandException(
                    LogMessage.CreateError(NuGetLogCode.NU3003,
                    string.Format(Strings.SignCommandMultipleCertException,
                    nameof(SignArgs.CertificateFingerprint))));
#endif
            }

            if (matchingCertCollection.Count == 0)
            {
                throw new SignCommandException(
                    LogMessage.CreateError(NuGetLogCode.NU3003,
                    Strings.SignCommandNoCertException));
            }

            return matchingCertCollection[0];
        }

        private static X509Certificate2Collection FilterCodeSigningCertificates(X509Certificate2Collection matchingCollection)
        {
            var filteredCollection = new X509Certificate2Collection();

            foreach (var certificate in matchingCollection)
            {
                if (SigningUtility.IsValidForPurposeFast(certificate, Oids.CodeSigningEkuOid))
                {
                    filteredCollection.Add(certificate);
                }
            }

            return filteredCollection;
        }
    }
}