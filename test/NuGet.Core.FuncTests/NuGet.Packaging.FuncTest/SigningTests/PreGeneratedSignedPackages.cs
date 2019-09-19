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
    public class PreGeneratedSignedPackages
    {
        private const string _untrustedChainCertError = "A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.";
        private const string _preGeneratePackageFolderName = "PreGenPackages";
        private readonly SignedPackageVerifierSettings _verifyCommandSettings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy(TestEnvironmentVariableReader.EmptyInstance);
        private readonly SignedPackageVerifierSettings _defaultSettings = SignedPackageVerifierSettings.GetDefault(TestEnvironmentVariableReader.EmptyInstance);
        private readonly SigningTestFixture _testFixture;
        private readonly TrustedTestCert<TestCertificate> _trustedTestCert;
        private readonly TestCertificate _untrustedTestCertificate;
        private readonly IList<ISignatureVerificationProvider> _trustProviders;
        private readonly X509Certificate2 _trustedRootCertForTSA;
        private string _dir;
        
        public PreGeneratedSignedPackages(SigningTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
            _untrustedTestCertificate = _testFixture.UntrustedTestCertificate;
            _trustProviders = new List<ISignatureVerificationProvider>()
            {
                new SignatureTrustAndValidityVerificationProvider()
            };
            _dir = CreatePreGenPackageForEachPlatform();

            //generate TSA root cert file under each platform 
            _trustedRootCertForTSA = _testFixture.TrustedServerRootCertificate;
            var tsaRootCertPath = new FileInfo(Path.Combine(_dir, "tsaRoot.cer"));
            var bytes = _trustedRootCertForTSA.RawData;
            File.WriteAllBytes(tsaRootCertPath.FullName, bytes);

        }
        [Fact]
        public async Task PreGenerateSignedPackages_AuthorSigned_TimeStamped()
        {
            // Arrange
            var caseName = "AuthorSigned_TimeStamped";

            var nupkg = new SimpleTestPackageContext(caseName);

            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
                        
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                string packagePath = Path.Combine(_dir, caseName, "package");
                Directory.CreateDirectory(packagePath);

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                testCertificate,
                nupkg,
                packagePath,
                timestampService.Url);

                //generate cert folder to store all certificate files.
                string certFolder = System.IO.Path.Combine(_dir, caseName, "cert");
                Directory.CreateDirectory(certFolder);

                //generate AuthorSigning cert files, path is certPath
                var authorCertFile = new FileInfo(Path.Combine(certFolder, "A.cer"));
                var Abytes = testCertificate.RawData;
                File.WriteAllBytes(authorCertFile.FullName, Abytes);

            }
        }
   
        private static string CreatePreGenPackageForEachPlatform()
        {
            var root = TestFileSystemUtility.NuGetTestFolder;
            var path = Path.Combine(root, _preGeneratePackageFolderName);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            //Create a folder for a each platform, under PreGenPackages folder.
            //For functional test on windows, 2 folders will be created.
            var platform = "";
#if IS_DESKTOP
            platform = "Windows_Net472";
#else
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                platform =  "Windows_NetStandard2.1";
            }
            else if (RuntimeEnvironmentHelper.IsMacOSX)
            {
                platform = "Mac_NetStandard2.1";
            }
            else
            {
                platform = "Linux_NetStandard2.1";
            }
#endif
            var pathForEachPlatform = Path.Combine(path, platform);

            if (Directory.Exists(pathForEachPlatform))
            {
                Directory.Delete(pathForEachPlatform, recursive: true);
            }
            Directory.CreateDirectory(pathForEachPlatform);

            return pathForEachPlatform;
        }
    }
}
#endif
