#if IS_SIGNING_SUPPORTED
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NuGet.Packaging.FuncTest;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;
namespace NuGet.Packaging.CrossVerify.Generate.Test
{
    [Collection(CrossVerifyCollection.Name)]
    public class GenerateSignedPackages
    {
        private readonly GenerateFixture _generateFixture;
        private string _dir;

        private readonly SigningTestFixture _signingTestFixture_Author;
        private readonly TrustedTestCert<TestCertificate> _authorSignningCert;
        private readonly X509Certificate2 _authorTSARootCert;

        private readonly SigningTestFixture _signingTestFixture_Repository;
        private readonly TrustedTestCert<TestCertificate> _repoSignningCert;
        private readonly X509Certificate2 _repoTSARootCert;
        
        public GenerateSignedPackages(GenerateFixture fixture)
        {
            _generateFixture = fixture;
            _dir = _generateFixture._dir;

            _signingTestFixture_Author = new SigningTestFixture();
            _authorSignningCert = _signingTestFixture_Author.TrustedTestCertificate;
            _authorTSARootCert = _signingTestFixture_Author.TrustedServerRootCertificate;

            _signingTestFixture_Repository = new SigningTestFixture();
            _repoSignningCert = _signingTestFixture_Repository.TrustedRepositoryCertificate;
            _repoTSARootCert = _signingTestFixture_Repository.TrustedServerRootCertificate;
        }
        [Fact]
        public async Task PreGenerateSignedPackages_AuthorSigned()
        {
            // Arrange
            var caseName = "A";
            string caseFolder = System.IO.Path.Combine(_dir, caseName);
            Directory.CreateDirectory(caseFolder);

            var nupkg = new SimpleTestPackageContext();

            using (var primaryCertificate = new X509Certificate2(_authorSignningCert.Source.Cert))
            {
                //Creat signed package under _dir\package folder
                string packagePath = Path.Combine(caseFolder, "package");
                Directory.CreateDirectory(packagePath);

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    primaryCertificate,
                    nupkg,
                    packagePath);

                //Creat certificate under _dir\cert folder
                string certFolder = System.IO.Path.Combine(caseFolder, "cert");
                Directory.CreateDirectory(certFolder);

                var CertFile = new FileInfo(Path.Combine(certFolder, "Author.cer"));
                var bytes = primaryCertificate.RawData;
                File.WriteAllBytes(CertFile.FullName, bytes);
            }
        }

        [Fact]
        public async Task PreGenerateSignedPackages_AuthorSigned_TimeStamped()
        {
            // Arrange
            var caseName = "AT";
            string caseFolder = System.IO.Path.Combine(_dir, caseName);
            Directory.CreateDirectory(caseFolder);

            var nupkg = new SimpleTestPackageContext();

            var timestampService = await _signingTestFixture_Author.GetDefaultTrustedTimestampServiceAsync();

            using (var primaryCertificate = new X509Certificate2(_authorSignningCert.Source.Cert))
            {
                string packagePath = Path.Combine(caseFolder, "package");
                Directory.CreateDirectory(packagePath);

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    primaryCertificate,
                    nupkg,
                    packagePath,
                    timestampService.Url);

                string certFolder = System.IO.Path.Combine(caseFolder, "cert");
                Directory.CreateDirectory(certFolder);

                var CertFile = new FileInfo(Path.Combine(certFolder, "Author.cer"));
                var certbytes = primaryCertificate.RawData;
                File.WriteAllBytes(CertFile.FullName, certbytes);

                var tsaRootCertPath = new FileInfo(Path.Combine(certFolder, "AuthorTSARoot.cer"));
                var tsaRootCertbytes = _authorTSARootCert.RawData;
                File.WriteAllBytes(tsaRootCertPath.FullName, tsaRootCertbytes);
            }
        }

        [Fact]
        public async Task PreGenerateSignedPackages_RepositorySigned()
        {
            // Arrange
            var caseName = "R";
            string caseFolder = System.IO.Path.Combine(_dir, caseName);
            Directory.CreateDirectory(caseFolder);

            var nupkg = new SimpleTestPackageContext();

            using (var primaryCertificate = new X509Certificate2(_repoSignningCert.Source.Cert))
            {
                string packagePath = Path.Combine(caseFolder, "package");
                Directory.CreateDirectory(packagePath);

                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    primaryCertificate,
                    nupkg,
                    packagePath,
                    new Uri("https://v3serviceIndex.test/api/index.json"));

                string certFolder = System.IO.Path.Combine(caseFolder, "cert");
                Directory.CreateDirectory(certFolder);

                var CertFile = new FileInfo(Path.Combine(certFolder, "Repo.cer"));
                var bytes = primaryCertificate.RawData;
                File.WriteAllBytes(CertFile.FullName, bytes);
            }
        }

        [Fact]
        public async Task PreGenerateSignedPackages_RepositorySigned_TimeStamped()
        {
            // Arrange
            var caseName = "RT";
            string caseFolder = System.IO.Path.Combine(_dir, caseName);
            Directory.CreateDirectory(caseFolder);

            var nupkg = new SimpleTestPackageContext();

            var timestampService = await _signingTestFixture_Repository.GetDefaultTrustedTimestampServiceAsync();

            using (var testCertificate = new X509Certificate2(_repoSignningCert.Source.Cert))
            {
                string packagePath = Path.Combine(caseFolder, "package");
                Directory.CreateDirectory(packagePath);

                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    testCertificate,
                    nupkg,
                    packagePath,
                    new Uri("https://v3serviceIndex.test/api/index.json"),
                    timestampService.Url);

                string certFolder = System.IO.Path.Combine(caseFolder, "cert");
                Directory.CreateDirectory(certFolder);

                var CertFile = new FileInfo(Path.Combine(certFolder, "Repo.cer"));
                var bytes = testCertificate.RawData;
                File.WriteAllBytes(CertFile.FullName, bytes);

                var tsaRootCertPath = new FileInfo(Path.Combine(certFolder, "RepoTSARoot.cer"));
                var tsaRootCertbytes = _repoTSARootCert.RawData;
                File.WriteAllBytes(tsaRootCertPath.FullName, tsaRootCertbytes);

            }
        }

        [Fact]
        public async Task PreGenerateSignedPackages_AuthorSigned_RepositoryCounterSigned()
        {
            // Arrange
            var caseName = "AR";
            string caseFolder = System.IO.Path.Combine(_dir, caseName);
            Directory.CreateDirectory(caseFolder);

            var nupkg = new SimpleTestPackageContext();

            using (var primaryCertificate = new X509Certificate2(_authorSignningCert.Source.Cert))
            using (var counterCertificate = new X509Certificate2(_repoSignningCert.Source.Cert))
            {
                string packagePath = Path.Combine(caseFolder, "package");
                Directory.CreateDirectory(packagePath);

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    primaryCertificate,
                    nupkg,
                    packagePath);

                var countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    counterCertificate, signedPackagePath,
                    packagePath,
                    new Uri("https://v3serviceIndex.test/api/index.json"));


                string certFolder = System.IO.Path.Combine(caseFolder, "cert");
                Directory.CreateDirectory(certFolder);

                var CertFile1 = new FileInfo(Path.Combine(certFolder, "Author.cer"));
                var bytes1 = primaryCertificate.RawData;
                File.WriteAllBytes(CertFile1.FullName, bytes1);

                var CertFile2 = new FileInfo(Path.Combine(certFolder, "Repo.cer"));
                var bytes2 = counterCertificate.RawData;
                File.WriteAllBytes(CertFile2.FullName, bytes2);
            }
        }


        [Fact]
        public async Task PreGenerateSignedPackages_AuthorSigned_Timestamped_RepositoryCounterSigned()
        {
            // Arrange
            var caseName = "ATR";
            string caseFolder = System.IO.Path.Combine(_dir, caseName);
            Directory.CreateDirectory(caseFolder);

            var nupkg = new SimpleTestPackageContext();

            var timestampServicePrimary = await _signingTestFixture_Author.GetDefaultTrustedTimestampServiceAsync();

            using (var primaryCertificate = new X509Certificate2(_authorSignningCert.Source.Cert))
            using (var counterCertificate = new X509Certificate2(_repoSignningCert.Source.Cert))
            {
                string packagePath = Path.Combine(caseFolder, "package");
                Directory.CreateDirectory(packagePath);

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    primaryCertificate,
                    nupkg,
                    packagePath,
                    timestampServicePrimary.Url);

                var countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    counterCertificate,
                    signedPackagePath,
                    packagePath,
                    new Uri("https://v3serviceIndex.test/api/index.json"));


                string certFolder = System.IO.Path.Combine(caseFolder, "cert");
                Directory.CreateDirectory(certFolder);

                var CertFile1 = new FileInfo(Path.Combine(certFolder, "Author.cer"));
                var bytes1 = primaryCertificate.RawData;
                File.WriteAllBytes(CertFile1.FullName, bytes1);

                var CertFile2 = new FileInfo(Path.Combine(certFolder, "Repo.cer"));
                var bytes2 = counterCertificate.RawData;
                File.WriteAllBytes(CertFile2.FullName, bytes2);

                var tsaRootCertPath = new FileInfo(Path.Combine(certFolder, "AuthorTSARoot.cer"));
                var tsaRootCertbytes = _authorTSARootCert.RawData;
                File.WriteAllBytes(tsaRootCertPath.FullName, tsaRootCertbytes);
            }
        }

        [Fact]
        public async Task PreGenerateSignedPackages_AuthorSigned_RepositoryCounterSigned_Timestamped()
        {
            // Arrange
            var caseName = "ART";
            string caseFolder = System.IO.Path.Combine(_dir, caseName);
            Directory.CreateDirectory(caseFolder);

            var nupkg = new SimpleTestPackageContext();

            var timestampServiceCounter = await _signingTestFixture_Repository.GetDefaultTrustedTimestampServiceAsync();

            using (var primaryCertificate = new X509Certificate2(_authorSignningCert.Source.Cert))
            using (var counterCertificate = new X509Certificate2(_repoSignningCert.Source.Cert))
            {
                string packagePath = Path.Combine(caseFolder, "package");
                Directory.CreateDirectory(packagePath);

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    primaryCertificate,
                    nupkg,
                    packagePath);

                var countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    counterCertificate, signedPackagePath,
                    packagePath,
                    new Uri("https://v3serviceIndex.test/api/index.json"),
                    timestampServiceCounter.Url);


                string certFolder = System.IO.Path.Combine(caseFolder, "cert");
                Directory.CreateDirectory(certFolder);

                var CertFile1 = new FileInfo(Path.Combine(certFolder, "Author.cer"));
                var bytes1 = primaryCertificate.RawData;
                File.WriteAllBytes(CertFile1.FullName, bytes1);

                var CertFile2 = new FileInfo(Path.Combine(certFolder, "Repo.cer"));
                var bytes2 = counterCertificate.RawData;
                File.WriteAllBytes(CertFile2.FullName, bytes2);

                var tsaRootCertPath = new FileInfo(Path.Combine(certFolder, "RepoTSARoot.cer"));
                var tsaRootCertbytes = _repoTSARootCert.RawData;
                File.WriteAllBytes(tsaRootCertPath.FullName, tsaRootCertbytes);

            }
        }

        [Fact]
        public async Task PreGenerateSignedPackages_AuthorSigned_Timestamped_RepositoryCounterSigned_Timestamped()
        {
            // Arrange
            var caseName = "ATRT";
            string caseFolder = System.IO.Path.Combine(_dir, caseName);
            Directory.CreateDirectory(caseFolder);

            var nupkg = new SimpleTestPackageContext();

            var timestampServicePrimary = await _signingTestFixture_Author.GetDefaultTrustedTimestampServiceAsync();
            var timestampServiceCounter = await _signingTestFixture_Repository.GetDefaultTrustedTimestampServiceAsync();

            using (var primaryCertificate = new X509Certificate2(_authorSignningCert.Source.Cert))
            using (var counterCertificate = new X509Certificate2(_repoSignningCert.Source.Cert))
            {
                string packagePath = Path.Combine(caseFolder, "package");
                Directory.CreateDirectory(packagePath);

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    primaryCertificate,
                    nupkg,
                    packagePath,
                    timestampServicePrimary.Url);

                var countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    counterCertificate, signedPackagePath,
                    packagePath,
                    new Uri("https://v3serviceIndex.test/api/index.json"),
                    timestampServiceCounter.Url);


                string certFolder = System.IO.Path.Combine(caseFolder, "cert");
                Directory.CreateDirectory(certFolder);

                var CertFile1 = new FileInfo(Path.Combine(certFolder, "Author.cer"));
                var bytes1 = primaryCertificate.RawData;
                File.WriteAllBytes(CertFile1.FullName, bytes1);

                var CertFile2 = new FileInfo(Path.Combine(certFolder, "Repo.cer"));
                var bytes2 = counterCertificate.RawData;
                File.WriteAllBytes(CertFile2.FullName, bytes2);

                var authorTsaRootCertPath = new FileInfo(Path.Combine(certFolder, "AuthorTSARoot.cer"));
                var authorTsaRootCertbytes = _authorTSARootCert.RawData;
                File.WriteAllBytes(authorTsaRootCertPath.FullName, authorTsaRootCertbytes);

                var repoTsaRootCertPath = new FileInfo(Path.Combine(certFolder, "RepoTSARoot.cer"));
                var repoTsaRootCertbytes = _repoTSARootCert.RawData;
                File.WriteAllBytes(repoTsaRootCertPath.FullName, repoTsaRootCertbytes);

            }
        }
    }
}
#endif


