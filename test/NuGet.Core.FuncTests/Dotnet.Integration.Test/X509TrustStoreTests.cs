// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using NuGet.Packaging;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Xunit;
using Xunit.Abstractions;

namespace Dotnet.Integration.Test
{
    [Collection(DotnetIntegrationCollection.Name)]
    public class X509TrustStoreTests
    {
        private readonly FileInfo _fallbackCertificateBundle;
        private readonly TestLogger _logger;

        public X509TrustStoreTests(MsbuildIntegrationTestFixture msbuildFixture, ITestOutputHelper helper)
        {
            _logger = new TestLogger(helper);

            _fallbackCertificateBundle = new FileInfo(
                Path.Combine(
                    msbuildFixture.SdkDirectory.FullName,
                    FallbackCertificateBundleX509ChainFactory.SubdirectoryName,
                    FallbackCertificateBundleX509ChainFactory.FileName));

            _logger.LogVerbose($"Expected fallback certificate bundle file path:  {_fallbackCertificateBundle.FullName}");
            _logger.LogVerbose($"Fallback certificate bundle file exists:  {_fallbackCertificateBundle.Exists}");
        }

        [PlatformFact(Platform.Windows)]
        public void CreateX509ChainFactoryForDotNetSdk_OnWindowsAlways_ReturnsInstance()
        {
            IX509ChainFactory factory = X509TrustStore.CreateX509ChainFactoryForDotNetSdk(
                _logger,
                _fallbackCertificateBundle);

            Assert.IsType<DotNetDefaultTrustStoreX509ChainFactory>(factory);

            // 1 message from the API under test and 2 messages from this test class's constructor
            Assert.Equal(3, _logger.Messages.Count);
            Assert.Equal(1, _logger.InformationMessages.Count);
            Assert.True(_logger.InformationMessages.TryPeek(out string actualMessage));
            Assert.Equal(Strings.ChainBuilding_UsingDefaultTrustStore, actualMessage);
        }

        [PlatformFact(Platform.Linux)]
        public void CreateX509ChainFactoryForDotNetSdk_OnLinuxAlways_ReturnsInstance()
        {
            IX509ChainFactory factory = X509TrustStore.CreateX509ChainFactoryForDotNetSdk(
                _logger,
                _fallbackCertificateBundle);

            bool wasFound = TryReadFirstBundle(
                SystemCertificateBundleX509ChainFactory.ProbePaths,
                out X509Certificate2Collection certificates,
                out string _);
            var certificateBundleFactory = (CertificateBundleX509ChainFactory)factory;

            if (wasFound)
            {
                Assert.IsType<SystemCertificateBundleX509ChainFactory>(factory);
                Assert.Equal(certificates.Count, certificateBundleFactory.Certificates.Count);
            }
            else
            {
                Assert.IsType<FallbackCertificateBundleX509ChainFactory>(factory);
                Assert.True(certificateBundleFactory.Certificates.Count > 0);
            }

            // 1 message from the API under test and 2 messages from this test class's constructor
            Assert.Equal(3, _logger.Messages.Count);
            Assert.Equal(1, _logger.InformationMessages.Count);
            Assert.True(_logger.InformationMessages.TryPeek(out string actualMessage));

            string expectedMessage;

            if (wasFound)
            {
                expectedMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.ChainBuilding_UsingSystemCertificateBundle,
                    certificateBundleFactory.FilePath);
            }
            else
            {
                expectedMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.ChainBuilding_UsingFallbackCertificateBundle,
                    certificateBundleFactory.FilePath);
            }

            Assert.Equal(expectedMessage, actualMessage);
        }

        [PlatformFact(Platform.Darwin)]
        public void CreateX509ChainFactoryForDotNetSdk_OnMacOsAlways_ReturnsInstance()
        {
            IX509ChainFactory factory = X509TrustStore.CreateX509ChainFactoryForDotNetSdk(
                _logger,
                _fallbackCertificateBundle);

            Assert.IsType<FallbackCertificateBundleX509ChainFactory>(factory);

            string expectedMessage = string.Format(
                CultureInfo.CurrentCulture,
                Strings.ChainBuilding_UsingFallbackCertificateBundle,
                ((CertificateBundleX509ChainFactory)factory).FilePath);

            // 1 message from the API under test and 2 messages from this test class's constructor
            Assert.Equal(3, _logger.Messages.Count);
            Assert.Equal(1, _logger.InformationMessages.Count);
            Assert.True(_logger.InformationMessages.TryPeek(out string actualMessage));
            Assert.Equal(expectedMessage, actualMessage);
        }

        private static bool TryReadFirstBundle(
            IReadOnlyList<string> probePaths,
            out X509Certificate2Collection certificates,
            out string successfulProbePath)
        {
            certificates = null;
            successfulProbePath = null;

            var oneProbePath = new string[1];

            foreach (string probePath in probePaths)
            {
                oneProbePath[0] = probePath;

                if (SystemCertificateBundleX509ChainFactory.TryCreate(
                    oneProbePath,
                    out SystemCertificateBundleX509ChainFactory factory))
                {
                    certificates = factory.Certificates;
                    successfulProbePath = probePath;

                    return true;
                }
            }

            return false;
        }
    }
}
