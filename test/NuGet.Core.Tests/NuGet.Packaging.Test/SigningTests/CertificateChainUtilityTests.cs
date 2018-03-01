// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class CertificateChainUtilityTests : IClassFixture<CertificatesFixture>
    {
        private readonly CertificatesFixture _fixture;

        public CertificateChainUtilityTests(CertificatesFixture fixture)
        {
            if (fixture == null)
            {
                throw new ArgumentNullException(nameof(fixture));
            }

            _fixture = fixture;
        }

        [Fact]
        public void GetCertificateChain_WhenCertificateNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => CertificateChainUtility.GetCertificateChain(
                    certificate: null,
                    extraStore: new X509Certificate2Collection(),
                    logger: NullLogger.Instance,
                    certificateType: CertificateType.Signature));

            Assert.Equal("certificate", exception.ParamName);
        }

        [Fact]
        public void GetCertificateChain_WhenExtraStoreNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => CertificateChainUtility.GetCertificateChain(
                    new X509Certificate2(),
                    extraStore: null,
                    logger: NullLogger.Instance,
                    certificateType: CertificateType.Signature));

            Assert.Equal("extraStore", exception.ParamName);
        }

        [Fact]
        public void GetCertificateChain_WhenLoggerNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => CertificateChainUtility.GetCertificateChain(
                    new X509Certificate2(),
                    new X509Certificate2Collection(),
                    logger: null,
                    certificateType: CertificateType.Signature));

            Assert.Equal("logger", exception.ParamName);
        }

        [Fact]
        public void GetCertificateChain_WhenCertificateTypeUndefined_Throws()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => CertificateChainUtility.GetCertificateChain(
                    new X509Certificate2(),
                    new X509Certificate2Collection(),
                    NullLogger.Instance,
                    (CertificateType)int.MaxValue));

            Assert.Equal("certificateType", exception.ParamName);
        }

        [Fact]
        public void GetCertificateChain_WithUntrustedRoot_Throws()
        {
            using (var chainHolder = new X509ChainHolder())
            using (var rootCertificate = SignTestUtility.GetCertificate("root.crt"))
            using (var intermediateCertificate = SignTestUtility.GetCertificate("intermediate.crt"))
            using (var leafCertificate = SignTestUtility.GetCertificate("leaf.crt"))
            {
                var chain = chainHolder.Chain;
                var extraStore = new X509Certificate2Collection() { rootCertificate, intermediateCertificate };
                var logger = new TestLogger();

                var exception = Assert.Throws<SignatureException>(
                    () => CertificateChainUtility.GetCertificateChain(
                        leafCertificate,
                        extraStore,
                        logger,
                        CertificateType.Signature));

                Assert.Equal(NuGetLogCode.NU3018, exception.Code);
                Assert.Equal("Certificate chain validation failed.", exception.Message);

                Assert.Equal(1, logger.Errors);
                Assert.Equal(RuntimeEnvironmentHelper.IsWindows ? 2 : 1, logger.Warnings);

                AssertUntrustedRoot(logger.LogMessages, LogLevel.Error);
                AssertOfflineRevocation(logger.LogMessages, LogLevel.Warning);

                if (RuntimeEnvironmentHelper.IsWindows)
                {
                    AssertRevocationStatusUnknown(logger.LogMessages, LogLevel.Warning);
                }
            }
        }

        [Fact]
        public void GetCertificateChain_WithUntrustedSelfIssuedCertificate_ReturnsChain()
        {
            using (var certificate = _fixture.GetDefaultCertificate())
            {
                var logger = new TestLogger();

                var chain = CertificateChainUtility.GetCertificateChain(
                    certificate,
                    new X509Certificate2Collection(),
                    logger,
                    CertificateType.Signature);

                Assert.Equal(1, chain.Count);
                Assert.NotSame(certificate, chain[0]);
                Assert.True(certificate.RawData.SequenceEqual(chain[0].RawData));

                Assert.Equal(0, logger.Errors);
                Assert.Equal(RuntimeEnvironmentHelper.IsWindows ? 1 : 2, logger.Warnings);

                AssertUntrustedRoot(logger.LogMessages, LogLevel.Warning);

                if (!RuntimeEnvironmentHelper.IsWindows)
                {
                    AssertOfflineRevocation(logger.LogMessages, LogLevel.Warning);
                }
            }
        }

        [Fact]
        public void GetCertificateListFromChain_WhenCertChainNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => CertificateChainUtility.GetCertificateListFromChain(certChain: null));

            Assert.Equal("certChain", exception.ParamName);
        }

        [Fact]
        public void GetCertificateListFromChain_ReturnsCertificatesInOrder()
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

        private static void AssertOfflineRevocation(IEnumerable<ILogMessage> issues, LogLevel logLevel)
        {
            string offlineRevocation;

            if (RuntimeEnvironmentHelper.IsWindows)
            {
                offlineRevocation = "The revocation function was unable to check revocation because the revocation server was offline.";
            }
            else
            {
                offlineRevocation = "unable to get certificate CRL";
            }

            Assert.Contains(issues, issue =>
                issue.Code == NuGetLogCode.NU3018 &&
                issue.Level == logLevel &&
                issue.Message == offlineRevocation);
        }

        private static void AssertRevocationStatusUnknown(IEnumerable<ILogMessage> issues, LogLevel logLevel)
        {
            Assert.Contains(issues, issue =>
                issue.Code == NuGetLogCode.NU3018 &&
                issue.Level == logLevel &&
                issue.Message == "The revocation function was unable to check revocation for the certificate.");
        }

        private static void AssertUntrustedRoot(IEnumerable<ILogMessage> issues, LogLevel logLevel)
        {
            string untrustedRoot;

            if (RuntimeEnvironmentHelper.IsWindows)
            {
                untrustedRoot = "A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.";
            }
            else
            {
                untrustedRoot = "certificate not trusted";
            }

            Assert.Contains(issues, issue =>
                issue.Code == NuGetLogCode.NU3018 &&
                issue.Level == logLevel &&
                issue.Message == untrustedRoot);
        }
    }
}