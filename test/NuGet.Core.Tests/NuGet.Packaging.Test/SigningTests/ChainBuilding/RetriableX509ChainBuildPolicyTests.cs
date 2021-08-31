// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class RetriableX509ChainBuildPolicyTests : IClassFixture<CertificatesFixture>
    {
        private readonly CertificatesFixture _fixture;

        public RetriableX509ChainBuildPolicyTests(CertificatesFixture fixture)
        {
            _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        }

        [PlatformFact(Platform.Windows)]
        public void Constructor_WhenRetryCountIsInvalid_Throws()
        {
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new RetriableX509ChainBuildPolicy(retryCount: 0, TimeSpan.MaxValue));

            Assert.Equal("retryCount", exception.ParamName);
        }

        [PlatformFact(Platform.Windows)]
        public void Constructor_WhenSleepIntervalIsInvalid_Throws()
        {
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new RetriableX509ChainBuildPolicy(retryCount: 1, TimeSpan.FromSeconds(-1)));

            Assert.Equal("sleepInterval", exception.ParamName);
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData(1, 0)]
        [InlineData(3, 700)]
        public void Constructor_WhenArgumentsAreValid_InitializesProperties(
            int expectedRetryCount,
            int expectedMilliseconds)
        {
            TimeSpan expectedSleepInterval = TimeSpan.FromSeconds(expectedMilliseconds);

            var policy = new RetriableX509ChainBuildPolicy(expectedRetryCount, expectedSleepInterval);

            Assert.Equal(expectedRetryCount, policy.RetryCount);
            Assert.Equal(expectedSleepInterval, policy.SleepInterval);
        }

        [PlatformFact(Platform.Windows)]
        public void Build_WhenBuildIsNull_Throws()
        {
            var policy = new RetriableX509ChainBuildPolicy(retryCount: 1, TimeSpan.Zero);

            using (X509Certificate2 certificate = _fixture.GetDefaultCertificate())
            {
                ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                    () => policy.Build(chain: null, certificate));

                Assert.Equal("chain", exception.ParamName);
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Create_WhenCertificateIsNull_Throws()
        {
            var policy = new RetriableX509ChainBuildPolicy(retryCount: 1, TimeSpan.Zero);

            using (var chain = new X509Chain())
            {
                ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                    () => policy.Build(chain, certificate: null));

                Assert.Equal("certificate", exception.ParamName);
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Build_WhenArgumentsAreValidAndBuildSucceeds_DoesNotRetry()
        {
            var policy = new RetriableX509ChainBuildPolicy(retryCount: 3, TimeSpan.FromSeconds(10));

            using (var chain = new X509Chain())
            using (X509Certificate2 certificate = GetTrustedCertificate())
            {
                Stopwatch stopwatch = Stopwatch.StartNew();

                bool actualResult = policy.Build(chain, certificate);

                Assert.True(actualResult);
                Assert.True(stopwatch.Elapsed < policy.SleepInterval);
            }
        }

        [PlatformFact(Platform.Windows)]
        public void Build_WhenArgumentsAreValidAndBuildFailureIsRetriable_Retries()
        {
            var policy = new RetriableX509ChainBuildPolicy(retryCount: 3, TimeSpan.FromMilliseconds(50));

            using (var chain = new X509Chain())
            using (X509Certificate2 certificate = _fixture.GetDefaultCertificate())
            {
                Stopwatch stopwatch = Stopwatch.StartNew();

                bool actualResult = policy.Build(chain, certificate);

                Assert.False(actualResult);
                Assert.True(stopwatch.Elapsed > TimeSpan.FromMilliseconds(policy.RetryCount * policy.SleepInterval.TotalMilliseconds));
            }
        }

        private static X509Certificate2 GetTrustedCertificate()
        {
            X509Certificate2 certificate;

            using (var store = new X509Store(StoreName.Root))
            {
                store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadOnly);

                certificate = store.Certificates[0];

                store.Close();
            }

            return certificate;
        }
    }
}
