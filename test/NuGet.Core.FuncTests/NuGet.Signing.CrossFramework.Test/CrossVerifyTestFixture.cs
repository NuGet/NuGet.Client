// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using NuGet.Test.Utility;
using Org.BouncyCastle.Asn1.X509;
using Test.Utility.Signing;

namespace NuGet.Signing.CrossFramework.Test
{
    public class CrossVerifyTestFixture : IDisposable
    {
#if IS_DESKTOP
        private const string DotnetExe = "dotnet.exe";
        internal string _dotnetExePath;
#else
        private const string NuGetExe = "NuGet.exe";
        internal string _nugetExePath;
#endif
        private TrustedTestCert<X509Certificate2> _trustedTimestampRoot;
        private Lazy<Task<SigningTestServer>> _testServer;
        private Lazy<Task<CertificateAuthority>> _defaultTrustedCertificateAuthority;
        private Lazy<Task<TimestampService>> _defaultTrustedTimestampService;
        private readonly DisposableList<IDisposable> _responders;
        private Lazy<Task<X509Certificate2>> _defaultAuthorSigningCertficate;
        private Lazy<Task<X509Certificate2>> _defaultRepositorySigningCertficate;
        private TrustedTestCert<TestCertificate> _trustedTestCert;

        public CrossVerifyTestFixture()
        {
#if IS_DESKTOP
            var patchedCliFolder = TestDotnetCLiUtility.CopyAndPatchLatestDotnetCli(sdkVersion: "5", sdkTfm: "net5.0");
            _dotnetExePath = Path.Combine(patchedCliFolder, DotnetExe);
#else
            var nugetExeFolder = TestFileSystemUtility.GetNuGetExeDirectoryInRepo();
            _nugetExePath = Path.Combine(nugetExeFolder, NuGetExe);
#endif
            _testServer = new Lazy<Task<SigningTestServer>>(SigningTestServer.CreateAsync);
            _defaultTrustedCertificateAuthority = new Lazy<Task<CertificateAuthority>>(CreateDefaultTrustedCertificateAuthorityAsync);
            _defaultTrustedTimestampService = new Lazy<Task<TimestampService>>(CreateDefaultTrustedTimestampServiceAsync);
            _responders = new DisposableList<IDisposable>();
            _defaultAuthorSigningCertficate = new Lazy<Task<X509Certificate2>>(CreateDefaultAuthorSigningCertificateAsync);
            _defaultRepositorySigningCertficate = new Lazy<Task<X509Certificate2>>(CreateDefaultRepositorySigningCertificateAsync);
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

        public async Task<X509Certificate2> GetDefaultAuthorSigningCertificateAsync()
        {
            return await _defaultAuthorSigningCertficate.Value;
        }

        public async Task<X509Certificate2> GetDefaultRepositorySigningCertificateAsync()
        {
            return await _defaultRepositorySigningCertficate.Value;
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

        private async Task<CertificateAuthority> CreateDefaultTrustedCertificateAuthorityAsync()
        {
            var testServer = await _testServer.Value;
            var rootCa = CertificateAuthority.Create(testServer.Url);
            var intermediateCa = rootCa.CreateIntermediateCertificateAuthority();
            var rootCertificate = new X509Certificate2(rootCa.Certificate.GetEncoded());

            _trustedTimestampRoot = TrustedTestCert.Create(
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

        private async Task<X509Certificate2> CreateDefaultAuthorSigningCertificateAsync()
        {
            var ca = await CreateDefaultTrustedCertificateAuthorityAsync();
            var keyPair = SigningTestUtility.GenerateKeyPair(publicKeyLength: 2048);
            var issueOptions = new IssueCertificateOptions()
            {
                KeyPair = keyPair,
                NotBefore = DateTimeOffset.UtcNow.AddSeconds(-2),
                NotAfter = DateTimeOffset.UtcNow.AddHours(1),
                SubjectName = new X509Name("CN=NuGet Cross Verify Test Author Signning Certificate")
            };
            var bcCertificate = ca.IssueCertificate(issueOptions);
            return CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair);
        }

        private async Task<X509Certificate2> CreateDefaultRepositorySigningCertificateAsync()
        {
            var ca = await CreateDefaultTrustedCertificateAuthorityAsync();
            var keyPair = SigningTestUtility.GenerateKeyPair(publicKeyLength: 2048);
            var issueOptions = new IssueCertificateOptions()
            {
                KeyPair = keyPair,
                NotBefore = DateTimeOffset.UtcNow.AddSeconds(-2),
                NotAfter = DateTimeOffset.UtcNow.AddHours(1),
                SubjectName = new X509Name("CN=NuGet Cross Verify Test Repository Signning Certificate")
            };
            var bcCertificate = ca.IssueCertificate(issueOptions);
            return CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair);
        }

        public void Dispose()
        {
            _trustedTimestampRoot?.Dispose();

            if (_testServer.IsValueCreated)
            {
                _testServer.Value.Result.Dispose();
            }

            if (_defaultAuthorSigningCertficate.IsValueCreated)
            {
                _defaultAuthorSigningCertficate.Value.Result.Dispose();
            }

            if (_defaultRepositorySigningCertficate.IsValueCreated)
            {
                _defaultRepositorySigningCertficate.Value.Result.Dispose();
            }

            _responders.Dispose();
        }
    }
}
