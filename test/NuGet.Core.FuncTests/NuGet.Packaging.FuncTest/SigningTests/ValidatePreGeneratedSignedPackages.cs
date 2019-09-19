#if IS_SIGNING_SUPPORTED
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto.Parameters;
using Test.Utility;
using Test.Utility.Signing;
using Xunit;
using BcAccuracy = Org.BouncyCastle.Asn1.Tsp.Accuracy;
using DotNetUtilities = Org.BouncyCastle.Security.DotNetUtilities;

namespace NuGet.Packaging.FuncTest
{
    [Collection(SigningTestCollection.Name)]
    public class ValidatePreGeneratedSignedPackages
    {
        //private const string _untrustedChainCertError = "A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.";
        private readonly SignedPackageVerifierSettings _verifyCommandSettings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy(TestEnvironmentVariableReader.EmptyInstance);
        private readonly SignedPackageVerifierSettings _defaultSettings = SignedPackageVerifierSettings.GetDefault(TestEnvironmentVariableReader.EmptyInstance);
        private readonly SigningTestFixture _testFixture;
        //private readonly TrustedTestCert<TestCertificate> _trustedTestCert;
        //private readonly TestCertificate _untrustedTestCertificate;
        private readonly IList<ISignatureVerificationProvider> _trustProviders;
        private string _dir;
        private X509Store _store;

        public ValidatePreGeneratedSignedPackages(SigningTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            // _trustedTestCert = _testFixture.TrustedTestCertificate;
            // _untrustedTestCertificate = _testFixture.UntrustedTestCertificate;
            _trustProviders = new List<ISignatureVerificationProvider>()
            {
                new SignatureTrustAndValidityVerificationProvider()
            };
            _dir = GetPreGenPackageRootPath();
            //_platformFolderList = Directory.GetDirectories(_dir);

        }

        [Theory]
        [MemberData(nameof(FolderForEachPlatform))]
        public async Task VerifySignaturesAsync_PreGenerateSignedPackages_AuthorSigned_TimeStamped(string dir)
        {
            // Arrange
            var caseName = "AuthorSigned_TimeStamped";

            var settings = new SignedPackageVerifierSettings(
            allowUnsigned: false,
            allowIllegal: false,
            allowUntrusted: false,
            allowIgnoreTimestamp: true,
            allowMultipleTimestamps: true,
            allowNoTimestamp: true,
            allowUnknownRevocation: true,
            reportUnknownRevocation: true,
            verificationTarget: VerificationTarget.All,
            signaturePlacement: SignaturePlacement.Any,
            repositoryCountersignatureVerificationBehavior: SignatureVerificationBehavior.IfExistsAndIsNecessary,
            revocationMode: RevocationMode.Online);


            var signedPackageFolder = Path.Combine(dir, caseName, "package");
            var signedPackagePath = Directory.GetFiles(signedPackageFolder).First();

            var certFolder = Path.Combine(dir, caseName, "cert");
            var certFile = Directory.GetFiles(certFolder).First();
      
            var tsaRootCertFile = Directory.GetFiles(dir, "tsaRoot.cer", SearchOption.TopDirectoryOnly).First();

            using (var testCertificate = new X509Certificate2(File.ReadAllBytes(certFile)))
            using (var tsaRootCertificate = new X509Certificate2(File.ReadAllBytes(tsaRootCertFile)))
            using (var packageReader = new PackageArchiveReader(signedPackagePath))
            {
                CreateCertificateStore();
                AddCertificateToStore(testCertificate);
                AddCertificateToStore(tsaRootCertificate);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                // Act
                var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                // Assert
                result.IsValid.Should().BeTrue();

                var sb = new System.Text.StringBuilder();
                foreach (var resultWithError in resultsWithErrors)
                {
                    foreach (var message in resultWithError.Issues)
                    {
                        sb.AppendLine(message.Message);
                    }

                }
                var msg = sb.ToString();
                resultsWithErrors.Count().Should().Be(0);
            }
        }


        private static string GetPreGenPackageRootPath()
        {
            var root = TestFileSystemUtility.NuGetTestFolder;
            var path = System.IO.Path.Combine(root, "PreGenPackages");
            return path;
        }

        private void CreateCertificateStore()
        {
            StoreName storeName = StoreName.Root;
            StoreLocation storeLocation = StoreLocation.LocalMachine;

            if (RuntimeEnvironmentHelper.IsLinux)
            {
                storeName = StoreName.Root;
                storeLocation = StoreLocation.CurrentUser;
            }

            _store = new X509Store(storeName, storeLocation);
        }
        private void AddCertificateToStore(X509Certificate2 cert)
        {
            _store.Open(OpenFlags.ReadWrite);
            _store.Add(cert);

        }
        public static TheoryData FolderForEachPlatform
        {
            get
            {
                /* should have 4 folders:
                    "Windows_NetStandard2.1",
                    "Windows_Net472",
                    "Mac_NetStandard2.1",
                    "Linux_NetStandard2.1",
                */
                var folders = new TheoryData<string>();
                foreach (var folder in Directory.GetDirectories(GetPreGenPackageRootPath()))
                {
                    folders.Add(folder);
                }

                return folders;
            }
        }
    }
}
#endif
