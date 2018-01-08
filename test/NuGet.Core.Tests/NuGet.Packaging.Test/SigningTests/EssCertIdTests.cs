// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Security.Cryptography;
using NuGet.Packaging.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class EssCertIdTests
    {
        [Fact]
        public void Read_WithInvalidAsn1_Throws()
        {
            Assert.Throws<CryptographicException>(
                () => EssCertId.Read(new byte[] { 0x30, 0x0b }));
        }

        [Fact]
        public void Read_WithValidInput_ReturnsEssCertId()
        {
            var essCertIdBytes = Asn1TestData.SigningCertificate.Skip(6).ToArray();
            var certificateHash = essCertIdBytes.Skip(5).Take(20).ToArray();
            var serialNumber = essCertIdBytes.Skip(161).Take(19).Reverse().ToArray();

            var essCertId = EssCertId.Read(essCertIdBytes);

            Assert.Equal(1, essCertId.IssuerSerial.GeneralNames.Count);
            Assert.Equal(
                "CN=Microsoft Time-Stamp PCA 2010, O=Microsoft Corporation, L=Redmond, S=Washington, C=US",
                essCertId.IssuerSerial.GeneralNames[0].DirectoryName.Name);
            SignTestUtility.VerifyByteArrays(certificateHash, essCertId.CertificateHash);
            SignTestUtility.VerifyByteArrays(serialNumber, essCertId.IssuerSerial.SerialNumber);
        }
    }
}