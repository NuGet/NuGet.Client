// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_SIGNING_SUPPORTED

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.FuncTest
{
    [Collection(SigningTestCollection.Name)]
    public class X509TrustStoreTests : IDisposable
    {
        private readonly TestLogger _logger;

        public X509TrustStoreTests()
        {
            _logger = new TestLogger();

            // For these tests, use whatever factory X509TrustStore creates by default.
            TestFallbackCertificateBundleX509ChainFactory.SetTryUseAsDefault(tryUseAsDefault: false);
        }

        public void Dispose()
        {
            TestFallbackCertificateBundleX509ChainFactory.SetTryUseAsDefault(tryUseAsDefault: true);

            GC.SuppressFinalize(this);
        }

        [Fact]
        public void InitializeForDotNetSdk_WhenArgumentIsNull_Throws()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                () => X509TrustStore.InitializeForDotNetSdk(logger: null));

            Assert.Equal("logger", exception.ParamName);
        }

        [Fact]
        public void CreateX509ChainFactory_Always_ReturnsInstance()
        {
            IX509ChainFactory factory = X509TrustStore.CreateX509ChainFactory(_logger);

            Assert.IsType<DotNetDefaultTrustStoreX509ChainFactory>(factory);
            Assert.Equal(1, _logger.Messages.Count);
            Assert.Equal(1, _logger.VerboseMessages.Count);
            Assert.True(_logger.VerboseMessages.TryDequeue(out string actualMessage));
            Assert.Equal(Strings.ChainBuilding_UsingDefaultTrustStore, actualMessage);
        }

        [PlatformFact(Platform.Windows)]
        public void CreateX509ChainFactoryForDotNetSdk_OnWindowsAlways_ReturnsInstance()
        {
            IX509ChainFactory factory = X509TrustStore.CreateX509ChainFactoryForDotNetSdk(_logger);

            Assert.IsType<DotNetDefaultTrustStoreX509ChainFactory>(factory);
            Assert.Equal(1, _logger.Messages.Count);
            Assert.Equal(1, _logger.VerboseMessages.Count);
            Assert.True(_logger.VerboseMessages.TryDequeue(out string actualMessage));
            Assert.Equal(Strings.ChainBuilding_UsingDefaultTrustStore, actualMessage);
        }

#if NET5_0_OR_GREATER
        [PlatformFact(Platform.Linux)]
        public void CreateX509ChainFactoryForDotNetSdk_OnLinuxAlways_ReturnsInstance()
        {
            IX509ChainFactory factory = X509TrustStore.CreateX509ChainFactoryForDotNetSdk(_logger);

            bool wasFound = TryReadFirstBundle(
                SystemCertificateBundleX509ChainFactory.ProbePaths,
                out X509Certificate2Collection certificates,
                out string _);
            CertificateBundleX509ChainFactory certificateBundleFactory = null;

            if (X509TrustStore.IsEnabled)
            {
                certificateBundleFactory = (CertificateBundleX509ChainFactory)factory;

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
            }
            else
            {
                Assert.IsType<DotNetDefaultTrustStoreX509ChainFactory>(factory);
            }

            Assert.Equal(1, _logger.Messages.Count);
            Assert.Equal(1, _logger.VerboseMessages.Count);
            Assert.True(_logger.VerboseMessages.TryDequeue(out string actualMessage));

            string expectedMessage;

            if (X509TrustStore.IsEnabled)
            {
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
            }
            else
            {
                expectedMessage = Strings.ChainBuilding_UsingDefaultTrustStore;
            }

            Assert.Equal(expectedMessage, actualMessage);
        }

        [PlatformFact(Platform.Darwin)]
        public void CreateX509ChainFactoryForDotNetSdk_OnMacOsAlways_ReturnsInstance()
        {
            IX509ChainFactory factory = X509TrustStore.CreateX509ChainFactoryForDotNetSdk(_logger);
            string expectedMessage;
            
            if (X509TrustStore.IsEnabled)
            {
                Assert.IsType<FallbackCertificateBundleX509ChainFactory>(factory);

                expectedMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.ChainBuilding_UsingFallbackCertificateBundle,
                    ((CertificateBundleX509ChainFactory)factory).FilePath);
            }
            else
            {
                Assert.IsType<DotNetDefaultTrustStoreX509ChainFactory>(factory);

                expectedMessage = Strings.ChainBuilding_UsingDefaultTrustStore;
            }

            Assert.Equal(1, _logger.Messages.Count);
            Assert.Equal(1, _logger.VerboseMessages.Count);
            Assert.True(_logger.VerboseMessages.TryDequeue(out string actualMessage));
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
#endif
    }
}
#endif
