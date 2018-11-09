// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.FuncTest
{
    [Collection(SigningTestCollection.Name)]
    public class TimestampTests
    {
        private readonly SigningTestFixture _testFixture;
        private readonly TrustedTestCert<TestCertificate> _trustedTestCert;

        public TimestampTests(SigningTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
        }

        [CIOnlyFact]
        public async Task Timestamp_Verify_WithOfflineRevocation_ReturnsCorrectFlagsAndLogsAsync()
        {
            var nupkg = new SimpleTestPackageContext();

            using (var testServer = await SigningTestServer.CreateAsync())
            using (var responders = new DisposableList<IDisposable>())
            using (var packageStream = await nupkg.CreateAsStreamAsync())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var ca = CreateOfflineRevocationCA(testServer, responders);
                var timestampService = TimestampService.Create(ca);

                responders.Add(testServer.RegisterResponder(timestampService));

                var timestampProvider = new Rfc3161TimestampProvider(timestampService.Url);

                var signature = await SignedArchiveTestUtility.CreateAuthorSignatureForPackageAsync(testCertificate, packageStream, timestampProvider);
                var timestamp = signature.Timestamps.First();

                var settings = new SignedPackageVerifierSettings(
                    allowUnsigned: false,
                    allowUntrusted: false,
                    allowIllegal: false,
                    allowIgnoreTimestamp: false,
                    allowMultipleTimestamps: false,
                    allowNoTimestamp: false,
                    allowUnknownRevocation: false,
                    reportUnknownRevocation: true,
                    verificationTarget: VerificationTarget.All,
                    signaturePlacement: SignaturePlacement.Any,
                    repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.Always,
                    revocationMode: RevocationMode.Online);

                var logs = new List<SignatureLog>();

                var result = timestamp.Verify(signature, settings, HashAlgorithmName.SHA256, logs);

                result.HasFlag(SignatureVerificationStatusFlags.UnknownRevocation).Should().BeTrue();

                var errors = logs.Where(l => l.Level == LogLevel.Error);
                errors.Count().Should().Be(RuntimeEnvironmentHelper.IsWindows ? 2 : 1);

                if (RuntimeEnvironmentHelper.IsWindows)
                {
                    errors.Should().Contain(w => w.Code == NuGetLogCode.NU3028 && w.Message.Contains("The revocation function was unable to check revocation because the revocation server could not be reached."));
                    errors.Should().Contain(w => w.Code == NuGetLogCode.NU3028 && w.Message.Contains("The revocation function was unable to check revocation for the certificate."));
                }
                else
                {
                    errors.Should().Contain(w => w.Code == NuGetLogCode.NU3028 && w.Message.Contains("unable to get certificate CRL"));
                }
            }
        }

        private CertificateAuthority CreateOfflineRevocationCA(ISigningTestServer testServer, DisposableList<IDisposable> responders)
        {
            var rootCa = CertificateAuthority.Create(testServer.Url);
            var intermediateCa = rootCa.CreateIntermediateCertificateAuthority();

            var rootCertificate = new X509Certificate2(rootCa.Certificate.GetEncoded());

            var trustedServerRoot = TrustedTestCert.Create(
                rootCertificate,
                StoreName.Root,
                StoreLocation.LocalMachine);

            var ca = intermediateCa;

            while (ca != null)
            {
                responders.Add(testServer.RegisterResponder(ca));

                ca = ca.Parent;
            }

            return intermediateCa;
        }
    }
}
#endif