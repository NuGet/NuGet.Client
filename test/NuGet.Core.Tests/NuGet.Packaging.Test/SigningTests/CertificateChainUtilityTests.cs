// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using NuGet.Common;
using NuGet.Packaging.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class CertificateChainUtilityTests
    {
        [Fact]
        public void GetCertificateChainForSigning_WhenCertificateNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => CertificateChainUtility.GetCertificateChainForSigning(certificate: null, extraStore: new X509Certificate2Collection(), certificateType: NuGetVerificationCertificateType.Signature));

            Assert.Equal("certificate", exception.ParamName);
        }

        [Fact]
        public void GetCertificateChainForSigning_WhenExtraStoreNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => CertificateChainUtility.GetCertificateChainForSigning(new X509Certificate2(), extraStore: null, certificateType: NuGetVerificationCertificateType.Signature));

            Assert.Equal("extraStore", exception.ParamName);
        }

        [Fact]
        public void GetCertificateChainForSigning_WithUntrustedRoot_Throws()
        {
            using (var chainHolder = new X509ChainHolder())
            using (var rootCertificate = SignTestUtility.GetCertificate("root.crt"))
            using (var intermediateCertificate = SignTestUtility.GetCertificate("intermediate.crt"))
            using (var leafCertificate = SignTestUtility.GetCertificate("leaf.crt"))
            {
                var chain = chainHolder.Chain;

                var extraStore = new X509Certificate2Collection
                {
                    rootCertificate,
                    intermediateCertificate
                };

                var exception = Assert.Throws<SignatureException>(
                    () => CertificateChainUtility.GetCertificateChainForSigning(leafCertificate, extraStore, NuGetVerificationCertificateType.Signature));

                Assert.Equal(NuGetLogCode.NU3018, exception.Code);
            }
        }

        [Fact]
        public void GetCertificateChainForSigning_ReturnsCertificatesInOrder()
        {
            using (var chainHolder = new X509ChainHolder())
            using (var rootCertificate = SignTestUtility.GetCertificate("root.crt"))
            using (var intermediateCertificate = SignTestUtility.GetCertificate("intermediate.crt"))
            using (var leafCertificate = SignTestUtility.GetCertificate("leaf.crt"))
            {
                var chain = chainHolder.Chain;

                chain.ChainPolicy.ExtraStore.Add(rootCertificate);
                chain.ChainPolicy.ExtraStore.Add(intermediateCertificate);

                chain.Build(leafCertificate);

                var certificateChain = CertificateChainUtility.GetCertificateListFromChain(chain);

                Assert.Equal(3, certificateChain.Count);
                Assert.Equal(leafCertificate.Thumbprint, certificateChain[0].Thumbprint);
                Assert.Equal(intermediateCertificate.Thumbprint, certificateChain[1].Thumbprint);
                Assert.Equal(rootCertificate.Thumbprint, certificateChain[2].Thumbprint);
            }
        }
    }
}