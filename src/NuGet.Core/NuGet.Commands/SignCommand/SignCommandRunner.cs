// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
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
        public int ExecuteCommand(SignArgs signArgs)
        {

            var success = true;

            // resolve path into multiple packages if needed.
            var packagesToSign = LocalFolderUtility.ResolvePackageFromPath(signArgs.PackagePath);
            LocalFolderUtility.EnsurePackageFileExists(signArgs.PackagePath, packagesToSign);

            var cert = GetCertificate(signArgs);

            signArgs.Logger.LogInformation(Environment.NewLine);
            signArgs.Logger.LogInformation(string.Format(CultureInfo.CurrentCulture,
                Strings.SignCommandDisplayCertificate,
                $"{Environment.NewLine}{CertificateUtility.X509Certificate2ToString(cert)}"));

            if (!string.IsNullOrEmpty(signArgs.Timestamper))
            {
                signArgs.Logger.LogInformation(string.Format(CultureInfo.CurrentCulture,
                    Strings.SignCommandDisplayTimestamper,
                    $"{Environment.NewLine}{signArgs.Timestamper}{Environment.NewLine}"));
            }

            if (!string.IsNullOrEmpty(signArgs.OutputDirectory))
            {
                signArgs.Logger.LogInformation(string.Format(CultureInfo.CurrentCulture,
                    Strings.SignCommandOutputPath,
                    $"{Environment.NewLine}{signArgs.OutputDirectory}{Environment.NewLine}"));
            }

            var signRequest = GenerateSignPackageRequest(signArgs, cert);
            var signatureProvider = GetSignatureProvider(signArgs);

            foreach (var packagePath in packagesToSign)
            {
                try
                {
                    string outputPath;

                    if (string.IsNullOrEmpty(signArgs.OutputDirectory))
                    {
                        outputPath = packagePath;
                    }
                    else
                    {
                        outputPath = Path.Combine(signArgs.OutputDirectory, Path.GetFileName(packagePath));
                    }

                    SignPackage(packagePath, outputPath, signArgs.Logger, signatureProvider, signRequest);
                }
                catch (Exception e)
                {
                    success = false;
                    ExceptionUtilities.LogException(e, signArgs.Logger);
                }
            }

            if (success)
            {
                signArgs.Logger.LogInformation(Strings.SignCommandSuccess);
            }

            return success ? 0 : 1;
        }

        private static ISignatureProvider GetSignatureProvider(SignArgs signArgs)
        {
            Rfc3161TimestampProvider timestampProvider = null;

            if (!string.IsNullOrEmpty(signArgs.Timestamper))
            {
                timestampProvider = new Rfc3161TimestampProvider(new Uri(signArgs.Timestamper));
            }

            return new X509SignatureProvider(timestampProvider);
        }

        private int SignPackage(string packagePath, string outputPath, ILogger logger, ISignatureProvider signatureProvider, SignPackageRequest request)
        {
            var tempFilePath = CopyPackage(packagePath);

            using (var packageWriteStream = File.Open(tempFilePath, FileMode.Open))
            using (var packageReadStream = File.OpenRead(packagePath))
            {
                var package = new SignedPackageArchive(packageReadStream, packageWriteStream);
                var signer = new Signer(package, signatureProvider);
                signer.SignAsync(request, logger, CancellationToken.None).Wait();
            }

            OverwritePackage(tempFilePath, outputPath);

            FileUtility.Delete(tempFilePath);

            return 0;
        }

        private static string CopyPackage(string sourceFilePath)
        {
            var destFilePath = Path.GetTempFileName();
            File.Copy(sourceFilePath, destFilePath, overwrite: true);

            return destFilePath;
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

        private static X509Certificate2 GetCertificate(SignArgs signArgs)
        {
            var certFindOptions = new CertificateSourceOptions()
            {
                CertificatePath = signArgs.CertificatePath,
                CertificatePassword = signArgs.CertificatePassword,
                Fingerprint = signArgs.CertificateFingerprint,
                StoreLocation = signArgs.CertificateStoreLocation,
                StoreName = signArgs.CertificateStoreName,
                SubjectName = signArgs.CertificateSubjectName
            };

            // get matching certificates
            var matchingCertCollection = CertificateProvider.GetCertificates(certFindOptions);

            if (matchingCertCollection.Count > 1)
            {
#if IS_DESKTOP
                if (signArgs.NonInteractive || !RuntimeEnvironmentHelper.IsWindows)
                {
                    // if on non-windows os or in non interactive mode - display the certs and error out
                    signArgs.Logger.LogInformation(CertificateUtility.X509Certificate2CollectionToString(matchingCertCollection));
                    throw new InvalidOperationException(string.Format(Strings.SignCommandMultipleCertException, nameof(SignArgs.CertificateFingerprint)));
                }
                else
                {
                    // Else launch UI to select
                    matchingCertCollection = X509Certificate2UI.SelectFromCollection(
                        matchingCertCollection,
                        Strings.SignCommandDialogTitle,
                        Strings.SignCommandDialogMessage,
                        X509SelectionFlag.SingleSelection);
                }
#else
                // if on non-windows os or in non interactive mode - display and error out
                signArgs.Logger.LogError(CertificateUtility.X509Certificate2CollectionToString(matchingCertCollection));
                throw new InvalidOperationException(string.Format(Strings.SignCommandMultipleCertException, nameof(SignArgs.CertificateFingerprint)));
#endif
            }

            if (matchingCertCollection.Count == 0)
            {
                throw new InvalidOperationException(Strings.SignCommandNoCertException);
            }

            return matchingCertCollection[0];
        }
    }
}
