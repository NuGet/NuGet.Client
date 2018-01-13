using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Test.Utility;
using Test.Utility.Signing;

namespace NuGet.Tests.Apex
{
    public class SignedPackagesTestsApexFixture : VisualStudioHostFixtureFactory
    {
        private const string _testPackageName = "TestPackage";
        private const string _expiredTestPackageName = "ExpiredTestPackage";
        private const string _packageVersion = "1.0.0";

        private TrustedTestCert<TestCertificate> _trustedTestCert;
        private TrustedTestCert<TestCertificate> _trustedExpiredTestCert;

        private SimpleTestPackageContext _signedTestPackageV1;
        private SimpleTestPackageContext _expiredSignedTestPackageV1;
        private string _expiredSignedTestPackageV1Path;
        private TestDirectory _expiredSignedTestPackageV1Directory;

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

        public SimpleTestPackageContext SignedTestPackage
        {
            get
            {
                if (_signedTestPackageV1 == null)
                {
                    _signedTestPackageV1 = Utils.CreatePackage(_testPackageName, _packageVersion);
                    _signedTestPackageV1.CertificateToSign = TrustedTestCertificate.Source.Cert;
                }

                return _signedTestPackageV1;
            }
        }

        public string ExpiredCertSignedTestPackagePath
        {
            get
            {
                if (_expiredSignedTestPackageV1Path == null)
                {
                    CreateSignedPackageWithExpiredCertificate();
                }

                return _expiredSignedTestPackageV1Path;
            }
        }

        public SimpleTestPackageContext ExpiredCertSignedTestPackage
        {
            get
            {
                if (_expiredSignedTestPackageV1 == null)
                {
                    CreateSignedPackageWithExpiredCertificate();
                }

                return _expiredSignedTestPackageV1;
            }
        }

        private void CreateSignedPackageWithExpiredCertificate()
        {
            _expiredSignedTestPackageV1Directory = TestDirectory.Create();
            _trustedExpiredTestCert = SigningTestUtility.GenerateTrustedTestCertificateThatExpiresIn5Seconds();

            _expiredSignedTestPackageV1 = Utils.CreatePackage(_expiredTestPackageName, _packageVersion);
            _expiredSignedTestPackageV1.CertificateToSign = _trustedExpiredTestCert.Source.Cert;

            SimpleTestPackageUtility.CreatePackages(_expiredSignedTestPackageV1Directory, _expiredSignedTestPackageV1);
            _expiredSignedTestPackageV1Path = Path.Combine(_expiredSignedTestPackageV1Directory, _expiredSignedTestPackageV1.PackageName);

            // Wait for cert to expire
            Thread.Sleep(5000);
        }

        public override void Dispose()
        {
            _trustedTestCert?.Dispose();
            _trustedExpiredTestCert?.Dispose();
            _expiredSignedTestPackageV1Directory?.Dispose();

            base.Dispose();
        }
    }
}
