// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Tests.Apex
{
    public class SignedPackagesTestsApexFixture : VisualStudioHostFixtureFactory
    {
        private const string _testPackageName = "TestPackage";
        private const string _expiredTestPackageName = "ExpiredTestPackage";
        private const string _packageVersion = "1.0.0";

        private TrustedTestCert<TestCertificate> _trustedAuthorTestCert;
        private TrustedTestCert<TestCertificate> _trustedRepositoryTestCert;
        private TrustedTestCert<TestCertificate> _trustedExpiredTestCert;

        private SimpleTestPackageContext _authorSignedTestPackageV1;
        private SimpleTestPackageContext _repoSignedTestPackageV1;
        private SimpleTestPackageContext _repoCountersignedTestPackageV1;

        private SimpleTestPackageContext _expiredAuthorSignedTestPackageV1;
        private string _expiredSignedTestPackageV1Path;
        private TestDirectory _expiredSignedTestPackageV1Directory;

        public TrustedTestCert<TestCertificate> TrustedAuthorTestCertificate
        {
            get
            {
                if (_trustedAuthorTestCert == null)
                {
                    _trustedAuthorTestCert = SigningTestUtility.GenerateTrustedTestCertificate();
                }

                return _trustedAuthorTestCert;
            }
        }

        public TrustedTestCert<TestCertificate> TrustedRepositoryTestCertificate
        {
            get
            {
                if (_trustedRepositoryTestCert == null)
                {
                    _trustedRepositoryTestCert = SigningTestUtility.GenerateTrustedTestCertificate();
                }

                return _trustedRepositoryTestCert;
            }
        }

        public SimpleTestPackageContext AuthorSignedTestPackage
        {
            get
            {
                if (_authorSignedTestPackageV1 == null)
                {
                    _authorSignedTestPackageV1 = Utils.CreateAuthorSignedPackage(
                        _testPackageName,
                        _packageVersion,
                        TrustedAuthorTestCertificate.Source.Cert);
                }

                return _authorSignedTestPackageV1;
            }
        }

        public SimpleTestPackageContext RepositorySignedTestPackage
        {
            get
            {
                if (_repoSignedTestPackageV1 == null)
                {
                    _repoSignedTestPackageV1 = Utils.CreateRepositorySignedPackage(
                        _testPackageName,
                        _packageVersion,
                        TrustedRepositoryTestCertificate.Source.Cert,
                        new Uri("https://v3serviceIndex.test/api/index.json"));
                }

                return _repoSignedTestPackageV1;
            }
        }

        public SimpleTestPackageContext RepositoryCountersignedTestPackage
        {
            get
            {
                if (_repoCountersignedTestPackageV1 == null)
                {
                    _repoCountersignedTestPackageV1 = Utils.CreateRepositoryCountersignedPackage(
                        _testPackageName,
                        _packageVersion,
                        TrustedAuthorTestCertificate.Source.Cert,
                        TrustedRepositoryTestCertificate.Source.Cert,
                        new Uri("https://v3serviceIndex.test/api/index.json"));
                }

                return _repoCountersignedTestPackageV1;
            }
        }

        public string ExpiredCertSignedTestPackagePath => _expiredSignedTestPackageV1Path;

        public SimpleTestPackageContext ExpiredCertSignedTestPackage => _expiredAuthorSignedTestPackageV1;

        public async Task CreateSignedPackageWithExpiredCertificateAsync()
        {
            _expiredSignedTestPackageV1Directory = TestDirectory.Create();
            _trustedExpiredTestCert = SigningTestUtility.GenerateTrustedTestCertificateThatExpiresIn5Seconds();

            _expiredAuthorSignedTestPackageV1 = Utils.CreatePackage(_expiredTestPackageName, _packageVersion);
            _expiredAuthorSignedTestPackageV1.PrimarySignatureCertificate = _trustedExpiredTestCert.Source.Cert;

            await SimpleTestPackageUtility.CreatePackagesAsync(_expiredSignedTestPackageV1Directory, _expiredAuthorSignedTestPackageV1);
            _expiredSignedTestPackageV1Path = Path.Combine(_expiredSignedTestPackageV1Directory, _expiredAuthorSignedTestPackageV1.PackageName);

            // Wait for cert to expire
            Thread.Sleep(5000);

            Assert.True(IsCertificateExpired(_trustedExpiredTestCert.Source.Cert));
        }

        private static bool IsCertificateExpired(X509Certificate2 certificate)
        {
            return DateTime.Now > certificate.NotAfter;
        }

        public override void Dispose()
        {
            _trustedAuthorTestCert?.Dispose();
            _trustedExpiredTestCert?.Dispose();
            _expiredSignedTestPackageV1Directory?.Dispose();

            base.Dispose();
        }
    }
}
