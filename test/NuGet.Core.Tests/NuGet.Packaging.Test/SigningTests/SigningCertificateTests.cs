// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using NuGet.Packaging.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class SigningCertificateTests
    {
        [Fact]
        public void Read_WithInvalidAsn1_Throws()
        {
            Assert.Throws<System.Security.Cryptography.CryptographicException>(
                () => SigningCertificate.Read(new byte[] { 0x30, 0x0b }));
        }

        [Fact]
        public void Read_WithValidInput_ReturnsSigningCertificate()
        {
            var signingCertificateBytes = Asn1TestData.SigningCertificate;
            var certificateHash1 = signingCertificateBytes.Skip(11).Take(20).ToArray();
            var serialNumber1 = signingCertificateBytes.Skip(167).Take(19).Reverse().ToArray();
            var certificateHash2 = signingCertificateBytes.Skip(190).ToArray();

            var signingCertificate = SigningCertificate.Read(signingCertificateBytes);

            Assert.Equal(2, signingCertificate.Certificates.Count);

            var essCertId = signingCertificate.Certificates[0];

            SignTestUtility.VerifyByteArrays(certificateHash1, essCertId.CertificateHash);
            Assert.Equal(1, essCertId.IssuerSerial.GeneralNames.Count);
            Assert.Equal(
                "CN=Microsoft Time-Stamp PCA 2010, O=Microsoft Corporation, L=Redmond, S=Washington, C=US",
                essCertId.IssuerSerial.GeneralNames[0].DirectoryName.Name);
            SignTestUtility.VerifyByteArrays(serialNumber1, essCertId.IssuerSerial.SerialNumber);

            essCertId = signingCertificate.Certificates[1];

            SignTestUtility.VerifyByteArrays(certificateHash2, essCertId.CertificateHash);
            Assert.Null(essCertId.IssuerSerial);
        }
    }
}