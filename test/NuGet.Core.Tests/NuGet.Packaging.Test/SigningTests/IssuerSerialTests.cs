// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Packaging.Signing;
using Org.BouncyCastle.Math;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class IssuerSerialTests : IClassFixture<CertificatesFixture>
    {
        private readonly CertificatesFixture _fixture;

        public IssuerSerialTests(CertificatesFixture fixture)
        {
            if (fixture == null)
            {
                throw new ArgumentNullException(nameof(fixture));
            }

            _fixture = fixture;
        }

        [Fact]
        public void Create_WhenCertificateNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => IssuerSerial.Create(certificate: null));

            Assert.Equal("certificate", exception.ParamName);
        }

        [Fact]
        public void Create_WithCertificate_InitializesFields()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var issuerSerial = IssuerSerial.Create(certificate);

                Assert.Equal(1, issuerSerial.GeneralNames.Count);
                Assert.Equal(certificate.IssuerName.Name, issuerSerial.GeneralNames[0].DirectoryName.Name);
                SignTestUtility.VerifyByteArrays(certificate.GetSerialNumber(), issuerSerial.SerialNumber);
            }
        }

        [Fact]
        public void Create_WithSmallSerialNumber_ReturnsIssuerSerial()
        {
            using (var certificate = SigningTestUtility.GenerateCertificate("test", generator =>
                {
                    generator.SetSerialNumber(BigInteger.One);
                }))
            {
                var issuerSerial = IssuerSerial.Create(certificate);

                SignTestUtility.VerifyByteArrays(certificate.GetSerialNumber(), issuerSerial.SerialNumber);
            }
        }

        [Fact]
        public void Create_WithLargePositiveSerialNumber_ReturnsIssuerSerial()
        {
            using (var certificate = SigningTestUtility.GenerateCertificate("test", generator =>
                {
                    generator.SetSerialNumber(BigInteger.ValueOf(long.MaxValue));
                }))
            {
                var issuerSerial = IssuerSerial.Create(certificate);

                SignTestUtility.VerifyByteArrays(certificate.GetSerialNumber(), issuerSerial.SerialNumber);
            }
        }
    }
}