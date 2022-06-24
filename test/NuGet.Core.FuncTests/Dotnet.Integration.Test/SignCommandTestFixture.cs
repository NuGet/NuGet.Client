// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility.Signing;

namespace Dotnet.Integration.Test
{
    /// <summary>
    /// Used to bootstrap functional tests for signing.
    /// </summary>
    public class SignCommandTestFixture : IDisposable
    {
        private const int _normalCertChainLength = 3;
        //setting up a short cert chain then it's easier to make it invalid.
        private const int _shortCertChainLength = 2;

        private TrustedTestCert<X509Certificate2> _trustedTimestampRoot;
        private TrustedTestCert<X509Certificate2> _untrustedSelfIssuedCertificateInCertificateStore;
        private List<X509StoreCertificate> _defaultCertificateChain;
        private List<X509StoreCertificate> _invalidEkuCertificateChain;
        private List<X509StoreCertificate> _expiredCertificateChain;
        private List<X509StoreCertificate> _notYetValidCertificateChain;
        private List<X509StoreCertificate> _revokedCertificateChain;
        private List<X509StoreCertificate> _revocationUnknownCertificateChain;
        private IList<ISignatureVerificationProvider> _trustProviders;
        private SigningSpecifications _signingSpecifications;
        private MockServer _crlServer;
        private bool _crlServerRunning;
        private object _crlServerRunningLock = new();
        private TestDirectory _testDirectory;
        private Lazy<Task<SigningTestServer>> _testServer;
        private Lazy<Task<CertificateAuthority>> _defaultTrustedCertificateAuthority;
        private Lazy<Task<TimestampService>> _defaultTrustedTimestampService;
        private readonly DisposableList<IDisposable> _responders;

        public IX509StoreCertificate DefaultCertificate
        {
            get
            {
                if (_defaultCertificateChain is null)
                {
                    _defaultCertificateChain = new List<X509StoreCertificate>();

                    using (IX509CertificateChain chain = SigningTestUtility.GenerateCertificateChainWithoutTrust(
                        _normalCertChainLength,
                        CrlServer.Uri,
                        TestDirectory.Path))
                    {
                        StoreLocation rootStoreLocation = CertificateStoreUtilities.GetTrustedCertificateStoreLocation(readOnly: false);

                        _defaultCertificateChain.Add(
                            new X509StoreCertificate(StoreLocation.CurrentUser, StoreName.My, new X509Certificate2(chain[0])));
                        _defaultCertificateChain.Add(
                            new X509StoreCertificate(StoreLocation.CurrentUser, StoreName.My, new X509Certificate2(chain[1])));
                        _defaultCertificateChain.Add(
                            new X509StoreCertificate(rootStoreLocation, StoreName.Root, new X509Certificate2(chain[2])));
                    }

                    SetUpCrlDistributionPoint();
                }

                return _defaultCertificateChain[0];
            }
        }

        public IX509StoreCertificate CertificateWithInvalidEku
        {
            get
            {
                if (_invalidEkuCertificateChain is null)
                {
                    _invalidEkuCertificateChain = new List<X509StoreCertificate>();

                    using (IX509CertificateChain chain = SigningTestUtility.GenerateCertificateChainWithoutTrust(
                        _normalCertChainLength,
                        CrlServer.Uri,
                        TestDirectory.Path,
                        configureLeafCrl: true,
                        leafCertificateActionGenerator: SigningTestUtility.CertificateModificationGeneratorForInvalidEkuCert))
                    { 
                        StoreLocation rootStoreLocation = CertificateStoreUtilities.GetTrustedCertificateStoreLocation(readOnly: false);

                        _invalidEkuCertificateChain.Add(
                            new X509StoreCertificate(StoreLocation.CurrentUser, StoreName.My, new X509Certificate2(chain[0])));
                        _invalidEkuCertificateChain.Add(
                            new X509StoreCertificate(StoreLocation.CurrentUser, StoreName.My, new X509Certificate2(chain[1])));
                        _invalidEkuCertificateChain.Add(
                            new X509StoreCertificate(rootStoreLocation, StoreName.Root, new X509Certificate2(chain[2])));
                    }

                    SetUpCrlDistributionPoint();
                }

                return _invalidEkuCertificateChain[0];
            }
        }

        public IX509StoreCertificate ExpiredCertificate
        {
            get
            {
                if (_expiredCertificateChain is null)
                {
                    _expiredCertificateChain = new List<X509StoreCertificate>();

                    Action<TestCertificateGenerator> actionGenerator = SigningTestUtility.CertificateModificationGeneratorForCertificateThatOnlyValidInSpecifiedPeriod(
                        notBefore: DateTime.UtcNow.AddSeconds(-10),
                        notAfter: DateTime.UtcNow.AddSeconds(-9));

                    using (IX509CertificateChain chain = SigningTestUtility.GenerateCertificateChainWithoutTrust(
                        _normalCertChainLength,
                        CrlServer.Uri,
                        TestDirectory.Path,
                        configureLeafCrl: true,
                        leafCertificateActionGenerator: actionGenerator))
                    {
                        StoreLocation rootStoreLocation = CertificateStoreUtilities.GetTrustedCertificateStoreLocation(readOnly: false);

                        _expiredCertificateChain.Add(
                            new X509StoreCertificate(StoreLocation.CurrentUser, StoreName.My, new X509Certificate2(chain[0])));
                        _expiredCertificateChain.Add(
                            new X509StoreCertificate(StoreLocation.CurrentUser, StoreName.My, new X509Certificate2(chain[1])));
                        _expiredCertificateChain.Add(
                            new X509StoreCertificate(rootStoreLocation, StoreName.Root, new X509Certificate2(chain[2])));
                    }

                    SetUpCrlDistributionPoint();
                }

                return _expiredCertificateChain[0];
            }
        }

        public IX509StoreCertificate NotYetValidCertificate
        {
            get
            {
                if (_notYetValidCertificateChain is null)
                {
                    _notYetValidCertificateChain = new List<X509StoreCertificate>();

                    Action<TestCertificateGenerator> actionGenerator = SigningTestUtility.CertificateModificationGeneratorForCertificateThatOnlyValidInSpecifiedPeriod(
                        notBefore: DateTime.UtcNow.AddMinutes(10),
                        notAfter: DateTime.UtcNow.AddMinutes(15));

                    using (IX509CertificateChain chain = SigningTestUtility.GenerateCertificateChainWithoutTrust(
                        _normalCertChainLength,
                        CrlServer.Uri,
                        TestDirectory.Path,
                        configureLeafCrl: true,
                        leafCertificateActionGenerator: actionGenerator))
                    {
                        StoreLocation rootStoreLocation = CertificateStoreUtilities.GetTrustedCertificateStoreLocation(readOnly: false);

                        _notYetValidCertificateChain.Add(
                            new X509StoreCertificate(StoreLocation.CurrentUser, StoreName.My, new X509Certificate2(chain[0])));
                        _notYetValidCertificateChain.Add(
                            new X509StoreCertificate(StoreLocation.CurrentUser, StoreName.My, new X509Certificate2(chain[1])));
                        _notYetValidCertificateChain.Add(
                            new X509StoreCertificate(rootStoreLocation, StoreName.Root, new X509Certificate2(chain[2])));
                    }

                    SetUpCrlDistributionPoint();
                }

                return _notYetValidCertificateChain[0];
            }
        }

        public IX509StoreCertificate RevokedCertificate
        {
            get
            {
                if (_revokedCertificateChain is null)
                {
                    _revokedCertificateChain = new List<X509StoreCertificate>();

                    using (IX509CertificateChain chain = SigningTestUtility.GenerateCertificateChainWithoutTrust(
                        _shortCertChainLength,
                        CrlServer.Uri,
                        TestDirectory.Path,
                        revokeEndCertificate: true))
                    {
                        StoreLocation rootStoreLocation = CertificateStoreUtilities.GetTrustedCertificateStoreLocation(readOnly: false);

                        _revokedCertificateChain.Add(
                            new X509StoreCertificate(StoreLocation.CurrentUser, StoreName.My, new X509Certificate2(chain[0])));
                        _revokedCertificateChain.Add(
                            new X509StoreCertificate(rootStoreLocation, StoreName.Root, new X509Certificate2(chain[1])));
                    }

                    SetUpCrlDistributionPoint();
                }

                return _revokedCertificateChain[0];
            }
        }

        public IX509StoreCertificate RevocationUnknownCertificate
        {
            get
            {
                if (_revocationUnknownCertificateChain is null)
                {
                    _revocationUnknownCertificateChain = new List<X509StoreCertificate>();

                    using (IX509CertificateChain chain = SigningTestUtility.GenerateCertificateChainWithoutTrust(
                        _shortCertChainLength,
                        CrlServer.Uri,
                        TestDirectory.Path,
                        configureLeafCrl: false))
                    {
                        StoreLocation rootStoreLocation = CertificateStoreUtilities.GetTrustedCertificateStoreLocation(readOnly: false);

                        _revocationUnknownCertificateChain.Add(
                            new X509StoreCertificate(StoreLocation.CurrentUser, StoreName.My, new X509Certificate2(chain[0])));
                        _revocationUnknownCertificateChain.Add(
                            new X509StoreCertificate(rootStoreLocation, StoreName.Root, new X509Certificate2(chain[1])));
                    }

                    SetUpCrlDistributionPoint();
                }

                return _revocationUnknownCertificateChain[0];
            }
        }

        public X509Certificate2 UntrustedSelfIssuedCertificateInCertificateStore
        {
            get
            {
                if (_untrustedSelfIssuedCertificateInCertificateStore == null)
                {
                    X509Certificate2 certificate = SigningTestUtility.GenerateSelfIssuedCertificate(isCa: false);

                    _untrustedSelfIssuedCertificateInCertificateStore = TrustedTestCert.Create(
                        certificate,
                        StoreName.My,
                        StoreLocation.CurrentUser);
                }

                return new X509Certificate2(_untrustedSelfIssuedCertificateInCertificateStore.Source);
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

        internal MockServer CrlServer
        {
            get
            {
                if (_crlServer == null)
                {
                    _crlServer = new MockServer();
                }

                return _crlServer;
            }
        }

        public TestDirectory TestDirectory
        {
            get
            {
                if (_testDirectory == null)
                {
                    _testDirectory = TestDirectory.Create();
                }

                return _testDirectory;
            }
        }

        public SignCommandTestFixture()
        {
            _testServer = new Lazy<Task<SigningTestServer>>(SigningTestServer.CreateAsync);
            _defaultTrustedCertificateAuthority = new Lazy<Task<CertificateAuthority>>(CreateDefaultTrustedCertificateAuthorityAsync);
            _defaultTrustedTimestampService = new Lazy<Task<TimestampService>>(CreateDefaultTrustedTimestampServiceAsync);
            _responders = new DisposableList<IDisposable>();
        }

        private void SetUpCrlDistributionPoint()
        {
            lock (_crlServerRunningLock)
            {
                if (!_crlServerRunning)
                {
                    CrlServer.Get.Add(
                        "/",
                        request =>
                        {
                            var urlSplits = request.RawUrl.Split('/').Where(s => !string.IsNullOrEmpty(s)).ToArray();
                            if (urlSplits.Length != 2 || !urlSplits[1].EndsWith(".crl"))
                            {
                                return new Action<HttpListenerResponse>(response =>
                                {
                                    response.StatusCode = 404;
                                });
                            }
                            else
                            {
                                var crlName = urlSplits[1];
                                var crlPath = Path.Combine(TestDirectory, crlName);
                                if (File.Exists(crlPath))
                                {
                                    return new Action<HttpListenerResponse>(response =>
                                    {
                                        response.ContentType = "application/pkix-crl";
                                        response.StatusCode = 200;
                                        var content = File.ReadAllBytes(crlPath);
                                        MockServer.SetResponseContent(response, content);
                                    });
                                }
                                else
                                {
                                    return new Action<HttpListenerResponse>(response =>
                                    {
                                        response.StatusCode = 404;
                                    });
                                }
                            }
                        });

                    CrlServer.Start();
                    _crlServerRunning = true;
                }
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

        public void Dispose()
        {
            _trustedTimestampRoot?.Dispose();
            _untrustedSelfIssuedCertificateInCertificateStore?.Dispose();

            DisposeX509StoreCertificates(_defaultCertificateChain);
            DisposeX509StoreCertificates(_invalidEkuCertificateChain);
            DisposeX509StoreCertificates(_expiredCertificateChain);
            DisposeX509StoreCertificates(_notYetValidCertificateChain);
            DisposeX509StoreCertificates(_revokedCertificateChain);
            DisposeX509StoreCertificates(_revocationUnknownCertificateChain);

            _crlServer?.Stop();
            _crlServer?.Dispose();
            _testDirectory?.Dispose();
            _responders.Dispose();

            if (_testServer.IsValueCreated)
            {
                _testServer.Value.Result.Dispose();
            }
        }

        private static void DisposeX509StoreCertificates(List<X509StoreCertificate> storeCertificates)
        {
            if (storeCertificates is not null)
            {
                foreach (X509StoreCertificate storeCertificate in storeCertificates)
                {
                    storeCertificate.Dispose();
                }
            }
        }

        private async Task<CertificateAuthority> CreateDefaultTrustedCertificateAuthorityAsync()
        {
            var testServer = await _testServer.Value;
            var rootCa = CertificateAuthority.Create(testServer.Url);
            var intermediateCa = rootCa.CreateIntermediateCertificateAuthority();
            var rootCertificate = new X509Certificate2(rootCa.Certificate.GetEncoded());
            StoreLocation storeLocation = CertificateStoreUtilities.GetTrustedCertificateStoreLocation();

            _trustedTimestampRoot = TrustedTestCert.Create(
                rootCertificate,
                StoreName.Root,
                storeLocation);

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
    }
}
