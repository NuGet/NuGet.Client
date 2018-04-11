// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NuGet.Packaging.Signing;
using Test.Utility.Signing;

namespace NuGet.Packaging.FuncTest
{
    /// <summary>
    /// Used to bootstrap functional tests for signing.
    /// </summary>
    public class SigningTestFixture : IDisposable
    {
        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private TrustedTestCert<TestCertificate> _trustedTestCertExpired;
        private TrustedTestCert<TestCertificate> _trustedTestCertNotYetValid;
        private TrustedTestCert<TestCertificate> _trustedTestCertWillExpireIn5Seconds;
        private TrustedTestCert<X509Certificate2> _trustedServerRoot;
        private TestCertificate _untrustedTestCert;
        private IReadOnlyList<TrustedTestCert<TestCertificate>> _trustedTestCertificateWithReissuedCertificate;
        private IList<ISignatureVerificationProvider> _trustProviders;
        private SigningSpecifications _signingSpecifications;
        private Lazy<Task<SigningTestServer>> _testServer;
        private Lazy<Task<CertificateAuthority>> _defaultTrustedCertificateAuthority;
        private Lazy<Task<TimestampService>> _defaultTrustedTimestampService;
        private readonly DisposableList<IDisposable> _responders;

        public SigningTestFixture()
        {
            _testServer = new Lazy<Task<SigningTestServer>>(SigningTestServer.CreateAsync);
            _defaultTrustedCertificateAuthority = new Lazy<Task<CertificateAuthority>>(CreateDefaultTrustedCertificateAuthorityAsync);
            _defaultTrustedTimestampService = new Lazy<Task<TimestampService>>(CreateDefaultTrustedTimestampServiceAsync);
            _responders = new DisposableList<IDisposable>();
        }

        public TrustedTestCert<TestCertificate> TrustedTestCertificate
        {
            get
            {
                if (_trustedTestCert == null)
                {
                    _trustedTestCert = SigningTestUtility.GenerateTrustedTestCertificate();
                }

                return _trustedTestCert;
            }
        }

        public TrustedTestCert<TestCertificate> TrustedTestCertificateExpired
        {
            get
            {
                if (_trustedTestCertExpired == null)
                {
                    _trustedTestCertExpired = SigningTestUtility.GenerateTrustedTestCertificateExpired();
                }

                return _trustedTestCertExpired;
            }
        }

        public TrustedTestCert<TestCertificate> TrustedTestCertificateNotYetValid
        {
            get
            {
                if (_trustedTestCertNotYetValid == null)
                {
                    _trustedTestCertNotYetValid = SigningTestUtility.GenerateTrustedTestCertificateNotYetValid();
                }

                return _trustedTestCertNotYetValid;
            }
        }

        // We should not memoize this call because it is a time sensitive operation
        public TrustedTestCert<TestCertificate> TrustedTestCertificateWillExpireIn5Seconds => SigningTestUtility.GenerateTrustedTestCertificateThatExpiresIn5Seconds();

        public IReadOnlyList<TrustedTestCert<TestCertificate>> TrustedTestCertificateWithReissuedCertificate
        {
            get
            {
                if (_trustedTestCertificateWithReissuedCertificate == null)
                {
                    var keyPair = SigningTestUtility.GenerateKeyPair(publicKeyLength: 2048);
                    var certificateName = TestCertificate.GenerateCertificateName();
                    var certificate1 = SigningTestUtility.GenerateCertificate(certificateName, keyPair);
                    var certificate2 = SigningTestUtility.GenerateCertificate(certificateName, keyPair);

                    var testCertificate1 = new TestCertificate() { Cert = certificate1 }.WithTrust(StoreName.Root, StoreLocation.LocalMachine);
                    var testCertificate2 = new TestCertificate() { Cert = certificate2 }.WithTrust(StoreName.Root, StoreLocation.LocalMachine);

                    _trustedTestCertificateWithReissuedCertificate = new[]
                    {
                        testCertificate1,
                        testCertificate2
                    };
                }

                return _trustedTestCertificateWithReissuedCertificate;
            }
        }

        public TestCertificate UntrustedTestCertificate
        {
            get
            {
                if (_untrustedTestCert == null)
                {
                    _untrustedTestCert = TestCertificate.Generate(SigningTestUtility.CertificateModificationGeneratorForCodeSigningEkuCert);
                }

                return _untrustedTestCert;
            }
        }

        public IList<ISignatureVerificationProvider> TrustProviders
        {
            get
            {
                if (_trustProviders == null)
                {
                    _trustProviders = new List<ISignatureVerificationProvider>()
                    {
                        new SignatureTrustAndValidityVerificationProvider(),
                        new IntegrityVerificationProvider()
                    };
                }

                return _trustProviders;
            }
        }

        public SigningSpecifications SigningSpecifications
        {
            get
            {
                if (_signingSpecifications == null)
                {
                    _signingSpecifications = SigningSpecifications.V1;
                }

                return _signingSpecifications;
            }
        }

        public async Task<ISigningTestServer> GetSigningTestServerAsync()
        {
            return await _testServer.Value;
        }

        public async Task<CertificateAuthority> GetDefaultTrustedCertificateAuthorityAsync()
        {
            return await _defaultTrustedCertificateAuthority.Value;
        }

        public async Task<TimestampService> GetDefaultTrustedTimestampServiceAsync()
        {
            return await _defaultTrustedTimestampService.Value;
        }

        private async Task<CertificateAuthority> CreateDefaultTrustedCertificateAuthorityAsync()
        {
            var testServer = await _testServer.Value;
            var rootCa = CertificateAuthority.Create(testServer.Url);
            var intermediateCa = rootCa.CreateIntermediateCertificateAuthority();
            var rootCertificate = new X509Certificate2(rootCa.Certificate.GetEncoded());

            _trustedServerRoot = TrustedTestCert.Create(
                rootCertificate,
                StoreName.Root,
                StoreLocation.LocalMachine);

            var ca = intermediateCa;

            while (ca != null)
            {
                _responders.Add(testServer.RegisterResponder(ca));
                _responders.Add(testServer.RegisterResponder(ca.OcspResponder));

                ca = ca.Parent;
            }

            return intermediateCa;
        }

        private async Task<TimestampService> CreateDefaultTrustedTimestampServiceAsync()
        {
            var testServer = await _testServer.Value;
            var ca = await _defaultTrustedCertificateAuthority.Value;
            var timestampService = TimestampService.Create(ca);

            _responders.Add(testServer.RegisterResponder(timestampService));

            return timestampService;
        }

        public void Dispose()
        {
            _trustedTestCert?.Dispose();
            _trustedTestCertExpired?.Dispose();
            _trustedTestCertNotYetValid?.Dispose();
            _trustedServerRoot?.Dispose();
            _responders.Dispose();

            if (_trustedTestCertificateWithReissuedCertificate != null)
            {
                foreach (var certificate in _trustedTestCertificateWithReissuedCertificate)
                {
                    certificate.Dispose();
                }
            }

            if (_testServer.IsValueCreated)
            {
                _testServer.Value.Result.Dispose();
            }
        }
    }
}