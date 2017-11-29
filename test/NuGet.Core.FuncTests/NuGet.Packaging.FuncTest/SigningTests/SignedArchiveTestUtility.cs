// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility.Signing;

namespace NuGet.Packaging.FuncTest
{
    internal static class SignedArchiveTestUtility
    {
        private const string _internalTimestampServer = "http://rfc3161.gtm.corp.microsoft.com/TSS/HttpTspServer";

        /// <summary>
        /// Generates a signed copy of a package and returns the path to that package
        /// </summary>
        /// <param name="testCert">Certificate to be used while signing the package</param>
        /// <param name="nupkg">Package to be signed</param>
        /// <param name="dir">Directory for placing the signed package</param>
        /// <returns>Path to the signed copy of the package</returns>
        public static async Task<string> CreateSignedPackageAsync(TrustedTestCert<TestCertificate> testCert, SimpleTestPackageContext nupkg, string dir)
        {
            var testLogger = new TestLogger();

            using (var zipWriteStream = nupkg.CreateAsStream())
            {
                var signedPackagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                using (var signPackage = new SignedPackageArchive(zipWriteStream))
                {
                    // Sign the package
                    await SignPackageAsync(testLogger, testCert.Source.Cert, signPackage);
                }

                zipWriteStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (Stream fileStream = File.OpenWrite(signedPackagePath))
                {
                    zipWriteStream.CopyTo(fileStream);
                }

                return signedPackagePath;
            }
        }

        /// <summary>
        /// Generates a signed copy of a package and returns the path to that package
        /// This method timestamps a package and should only be used with tests marked with [CIOnlyFact]
        /// </summary>
        /// <param name="testCert">Certificate to be used while signing the package</param>
        /// <param name="nupkg">Package to be signed</param>
        /// <returns>Path to the signed copy of the package</returns>
        public static async Task<string> CreateSignedAndTimeStampedPackageAsync(TrustedTestCert<TestCertificate> testCert, SimpleTestPackageContext nupkg, string dir)
        {
            var testLogger = new TestLogger();

            using (var zipWriteStream = nupkg.CreateAsStream())
            {
                var signedPackagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                using (var signPackage = new SignedPackageArchive(zipWriteStream))
                {
                    // Sign the package
                    await SignAndTimeStampPackageAsync(testLogger, testCert.Source.Cert, signPackage);
                }

                zipWriteStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (Stream fileStream = File.OpenWrite(signedPackagePath))
                {
                    zipWriteStream.CopyTo(fileStream);
                }

                return signedPackagePath;
            }
        }

        /// <summary>
        /// unsigns a package for test purposes.
        /// This does not timestamp a signature and can be used outside corp network.
        /// </summary>
        public static async Task UnsignPackageAsync(string signedPackagePath, string dir)
        {
            var testLogger = new TestLogger();
            var testSignatureProvider = new X509SignatureProvider(timestampProvider: null);

            var copiedSignedPackagePath = Path.Combine(dir, Guid.NewGuid().ToString());
            File.Copy(signedPackagePath, copiedSignedPackagePath, overwrite: true);

            using (var zipWriteStream = File.Open(copiedSignedPackagePath, FileMode.Open))
            using (var signedPackage = new SignedPackageArchive(zipWriteStream))
            {
                var signer = new Signer(signedPackage, testSignatureProvider);
                await signer.RemoveSignaturesAsync(testLogger, CancellationToken.None);
            }

            File.Copy(copiedSignedPackagePath, signedPackagePath, overwrite: true);
        }

        /// <summary>
        /// Sign a package for test purposes.
        /// This does not timestamp a signature and can be used outside corp network.
        /// </summary>
        private static async Task SignPackageAsync(TestLogger testLogger, X509Certificate2 cert, SignedPackageArchive signPackage)
        {
            var testSignatureProvider = new X509SignatureProvider(timestampProvider: null);
            var signer = new Signer(signPackage, testSignatureProvider);

            var request = new SignPackageRequest()
            {
                Certificate = cert,
                SignatureHashAlgorithm = Common.HashAlgorithmName.SHA256
            };

            await signer.SignAsync(request, testLogger, CancellationToken.None);
        }

        /// <summary>
        /// Sign and timestamp a package for test purposes.
        /// This method timestamps a package and should only be used with tests marked with [CIOnlyFact]
        /// </summary>
        private static async Task SignAndTimeStampPackageAsync(TestLogger testLogger, X509Certificate2 cert, SignedPackageArchive signPackage)
        {
            var testSignatureProvider = new X509SignatureProvider(new Rfc3161TimestampProvider(new Uri(_internalTimestampServer)));
            var signer = new Signer(signPackage, testSignatureProvider);

            var request = new SignPackageRequest()
            {
                Certificate = cert,
                SignatureHashAlgorithm = Common.HashAlgorithmName.SHA256
            };

            await signer.SignAsync(request, testLogger, CancellationToken.None);
        }


        public static async Task<VerifySignaturesResult> VerifySignatureAsync(SignedPackageArchive signPackage, SignedPackageVerifierSettings settings)
        {
            var verificationProviders = new[] { new X509SignatureVerificationProvider() };
            var verifier = new PackageSignatureVerifier(verificationProviders, settings);
            var result = await verifier.VerifySignaturesAsync(signPackage, CancellationToken.None);
            return result;
        }
    }
}
