// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Packaging.Test
{
    public static class SignTestUtility
    {
        public static async Task<VerifySignaturesResult> VerifySignatureAsync(SignedPackageArchive signPackage, SignedPackageVerifierSettings settings)
        {
            var verificationProviders = new[] { new SignatureTrustAndValidityVerificationProvider() };
            var verifier = new PackageSignatureVerifier(verificationProviders);
            var result = await verifier.VerifySignaturesAsync(signPackage, settings, CancellationToken.None);
            return result;
        }

        internal static byte[] GetResourceBytes(string name)
        {
            return ResourceTestUtility.GetResourceBytes($"NuGet.Packaging.Test.compiler.resources.{name}", typeof(SignTestUtility));
        }

        internal static X509Certificate2 GetCertificate(string name)
        {
            var bytes = GetResourceBytes(name);

            return new X509Certificate2(bytes);
        }

        internal static byte[] GetHash(X509Certificate2 certificate, HashAlgorithmName hashAlgorithm)
        {
            return hashAlgorithm.ComputeHash(certificate.RawData);
        }

        internal static void VerifySerialNumber(X509Certificate2 certificate, IssuerSerial issuerSerial)
        {
            var serialNumber = certificate.GetSerialNumber();

            // Convert from little endian to big endian.
            Array.Reverse(serialNumber);

            VerifyByteArrays(serialNumber, issuerSerial.SerialNumber);
        }

        internal static void VerifyByteArrays(byte[] expected, byte[] actual)
        {
            var expectedHex = BitConverter.ToString(expected).Replace("-", "");
            var actualHex = BitConverter.ToString(actual).Replace("-", "");

            Assert.Equal(expectedHex, actualHex);
        }
    }
}