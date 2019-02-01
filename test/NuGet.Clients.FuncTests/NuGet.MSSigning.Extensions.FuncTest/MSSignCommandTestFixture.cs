// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NuGet.CommandLine.Test;
using NuGet.Common;
using NuGet.Test.Utility;
using Test.Utility.Signing;

namespace NuGet.MSSigning.Extensions.FuncTest.Commands
{
    /// <summary>
    /// Used to bootstrap functional tests for signing.
    /// </summary>
    public class MSSignCommandTestFixture : IDisposable
    {
        private TrustedTestCert<TestCertificate> _trustedTestCertWithPrivateKey;
        private TrustedTestCert<TestCertificate> _trustedTestCertWithoutPrivateKey;
        private TrustedTestCert<X509Certificate2> _trustedTimestampRoot;

        private string _nugetExePath;
        private Lazy<Task<SigningTestServer>> _testServer;
        private Lazy<Task<CertificateAuthority>> _defaultTrustedCertificateAuthority;
        private Lazy<Task<TimestampService>> _defaultTrustedTimestampService;
        private readonly DisposableList<IDisposable> _responders;
        private readonly TestDirectory _certDir;

        public MSSignCommandTestFixture()
        {
            _testServer = new Lazy<Task<SigningTestServer>>(SigningTestServer.CreateAsync);
            _defaultTrustedCertificateAuthority = new Lazy<Task<CertificateAuthority>>(CreateDefaultTrustedCertificateAuthorityAsync);
            _defaultTrustedTimestampService = new Lazy<Task<TimestampService>>(CreateDefaultTrustedTimestampServiceAsync);
            _responders = new DisposableList<IDisposable>();
            _certDir = TestDirectory.Create();
        }

        public TrustedTestCert<TestCertificate> TrustedTestCertificateWithPrivateKey
        {
            get
            {
                if (_trustedTestCertWithPrivateKey == null)
                {
                    var actionGenerator = SigningTestUtility.CertificateModificationGeneratorForCodeSigningEkuCert;

                    // Code Sign EKU needs trust to a root authority
                    // Add the cert to Root CA list in LocalMachine as it does not prompt a dialog
                    // This makes all the associated tests to require admin privilege
                    _trustedTestCertWithPrivateKey = TestCertificate.Generate(actionGenerator).WithPrivateKeyAndTrust(StoreName.Root, StoreLocation.LocalMachine, _certDir);
                }

                return _trustedTestCertWithPrivateKey;
            }
        }

        public TrustedTestCert<TestCertificate> TrustedTestCertificateWithoutPrivateKey
        {
            get
            {
                if (_trustedTestCertWithoutPrivateKey == null)
                {
                    var actionGenerator = SigningTestUtility.CertificateModificationGeneratorForCodeSigningEkuCert;

                    // Code Sign EKU needs trust to a root authority
                    // Add the cert to Root CA list in LocalMachine as it does not prompt a dialog
                    // This makes all the associated tests to require admin privilege
                    _trustedTestCertWithoutPrivateKey = TestCertificate.Generate(actionGenerator).WithPrivateKeyAndTrust(StoreName.Root, StoreLocation.LocalMachine, _certDir);

                }

                return _trustedTestCertWithoutPrivateKey;
            }
        }

        public string NuGetExePath
        {
            get
            {
                if (_nugetExePath == null)
                {
                    _nugetExePath = Util.GetNuGetExePath();
                }

                return _nugetExePath;
            }
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
            var rootCertificate = new X509Certificate2(rootCa.Certificate.RawData);

            _trustedTimestampRoot = TrustedTestCert.Create(
                rootCertificate,
                StoreName.Root,
                StoreLocation.LocalMachine,
                _certDir);

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
            _trustedTestCertWithPrivateKey?.Dispose();
            _trustedTestCertWithoutPrivateKey?.Dispose();
            _trustedTimestampRoot?.Dispose();
            _responders.Dispose();
            _certDir.Dispose();

            if (_testServer.IsValueCreated)
            {
                _testServer.Value.Result.Dispose();
            }
        }
    }
}
