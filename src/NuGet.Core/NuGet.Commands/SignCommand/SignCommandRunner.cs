// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Protocol.Core.Types;

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
            var packagesToSign = PackageUpdateResource.ResolvePackageFromPath(signArgs.PackagePath);
            PackageUpdateResource.EnsurePackageFileExists(signArgs.PackagePath, packagesToSign);

            var cert = GetCertificate(signArgs);

            signArgs.Logger.LogInformation(string.Format(CultureInfo.CurrentCulture,
                Strings.SignCommandDisplayCertificate,
                CertificateUtility.X509Certificate2ToString(cert)));

            var signRequest = GenerateSignPackageRequest(signArgs, cert);

            foreach (var packagePath in packagesToSign)
            {
                try
                {
                    SignPackage(packagePath, signArgs.Logger, signRequest);
                }
                catch(Exception e)
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

        private int SignPackage(string path, ILogger logger, SignPackageRequest request)
        {
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
            {
                var package = new SignedPackageArchive(zip);
                var signer = new Signer(package);
                signer.SignAsync(request, logger, CancellationToken.None).Wait();
            }

            return 0;
        }

        private SignPackageRequest GenerateSignPackageRequest(SignArgs signArgs, X509Certificate2 certificate)
        {
            return new SignPackageRequest()
            {
                Signature = new Signature()
                {
                    DisplayName = "FakeSignature",
                    Type = SignatureType.Author,
                    TestTrust = SignatureVerificationStatus.Trusted
                }
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
#if !IS_CORECLR
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
