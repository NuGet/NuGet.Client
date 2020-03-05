// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
using NuGet.Packaging.FuncTest;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.CrossVerify.Verify.Test
{
    [Collection(SigningTestCollection.Name)]
    public class VerifySignedPackages
    {
        private readonly IList<ISignatureVerificationProvider> _trustProviders;
        private string _dir;

        public VerifySignedPackages()
        {
            _trustProviders = new List<ISignatureVerificationProvider>()
            {
                new IntegrityVerificationProvider(),
                new SignatureTrustAndValidityVerificationProvider()
            };
            _dir = GetGeneratedPackagesRootPath();
        }

        [Theory]
        [MemberData(nameof(FolderForEachPlatform))]
        public async Task VerifySignaturesAsync_PreGenerateSignedPackages_AuthorSigned(string dir)
        {
            // Arrange
            var caseName = "A";

            var settings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy();

            var signedPackageFolder = Path.Combine(dir, caseName, "package");
            var signedPackagePath = Directory.GetFiles(signedPackageFolder).Where(f => f.EndsWith(".nupkg")).First();

            var certFolder = Path.Combine(dir, caseName, "cert");
            var authorCertFile = Directory.GetFiles(certFolder).Where(f => f.EndsWith("Author.cer")).First();

            using (var primaryCertificate = new X509Certificate2(File.ReadAllBytes(authorCertFile)))
            using (var packageReader = new PackageArchiveReader(signedPackagePath))
            using (var store = new X509Store(StoreName.Root,
                RuntimeEnvironmentHelper.IsWindows ? StoreLocation.LocalMachine : StoreLocation.CurrentUser))
            {
                AddCertificateToStore(primaryCertificate, store);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                // Act
                var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());

                // Assert
                try
                {
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                    resultsWithWarnings.Count().Should().Be(0);
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message + GetResultIssues(result, dir));
                }
            }
        }

        [Theory]
        [MemberData(nameof(FolderForEachPlatform))]
        public async Task VerifySignaturesAsync_PreGenerateSignedPackages_AuthorSigned_TimeStamped(string dir)
        {
            // Arrange
            var caseName = "AT";

            var settings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy();

            var signedPackageFolder = Path.Combine(dir, caseName, "package");
            var signedPackagePath = Directory.GetFiles(signedPackageFolder).Where(f => f.EndsWith(".nupkg")).First();

            var certFolder = Path.Combine(dir, caseName, "cert");
            var authorCertFile = Directory.GetFiles(certFolder).Where(f => f.EndsWith("Author.cer")).First();
            var authorTsaRootCertFile = Directory.GetFiles(certFolder).Where(f => f.EndsWith("AuthorTSARoot.cer")).First();

            using (var primaryCertificate = new X509Certificate2(File.ReadAllBytes(authorCertFile)))
            using (var tsaRootCertificate = new X509Certificate2(File.ReadAllBytes(authorTsaRootCertFile)))
            using (var packageReader = new PackageArchiveReader(signedPackagePath))
            using (var store = new X509Store(StoreName.Root,
                RuntimeEnvironmentHelper.IsWindows ? StoreLocation.LocalMachine : StoreLocation.CurrentUser))
            {
                AddCertificateToStore(primaryCertificate, store);
                AddCertificateToStore(tsaRootCertificate, store);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                // Act
                var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());

                // Assert
                try
                {
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                    resultsWithWarnings.Count().Should().Be(0);
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message + GetResultIssues(result, dir));
                }
            }
        }

        [Theory]
        [MemberData(nameof(FolderForEachPlatform))]
        public async Task VerifySignaturesAsync_PreGenerateSignedPackages_RepositorySigned(string dir)
        {
            // Arrange
            var caseName = "R";

            var settings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy();

            var signedPackageFolder = Path.Combine(dir, caseName, "package");
            var signedPackagePath = Directory.GetFiles(signedPackageFolder).Where(f => f.EndsWith(".nupkg")).First();

            var certFolder = Path.Combine(dir, caseName, "cert");
            var repoCertFile = Directory.GetFiles(certFolder).Where(f => f.EndsWith("Repo.cer")).First();

            using (var primaryCertificate = new X509Certificate2(File.ReadAllBytes(repoCertFile)))
            using (var packageReader = new PackageArchiveReader(signedPackagePath))
            using (var store = new X509Store(StoreName.Root,
                RuntimeEnvironmentHelper.IsWindows ? StoreLocation.LocalMachine : StoreLocation.CurrentUser))
            {
                AddCertificateToStore(primaryCertificate, store);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                // Act
                var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());

                // Assert
                try
                {
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                    resultsWithWarnings.Count().Should().Be(0);
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message + GetResultIssues(result, dir));
                }
            }
        }

        [Theory]
        [MemberData(nameof(FolderForEachPlatform))]
        public async Task VerifySignaturesAsync_PreGenerateSignedPackages_RepositorySigned_Timestamped(string dir)
        {
            // Arrange
            var caseName = "RT";

            var settings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy();

            var signedPackageFolder = Path.Combine(dir, caseName, "package");
            var signedPackagePath = Directory.GetFiles(signedPackageFolder).Where(f => f.EndsWith(".nupkg")).First();

            var certFolder = Path.Combine(dir, caseName, "cert");
            var repoCertFile = Directory.GetFiles(certFolder).Where(f => f.EndsWith("Repo.cer")).First();
            var repoTsaRootCertFile = Directory.GetFiles(certFolder).Where(f => f.EndsWith("RepoTSARoot.cer")).First();

            using (var primaryCertificate = new X509Certificate2(File.ReadAllBytes(repoCertFile)))
            using (var tsaRootCertificate = new X509Certificate2(File.ReadAllBytes(repoTsaRootCertFile)))
            using (var packageReader = new PackageArchiveReader(signedPackagePath))
            using (var store = new X509Store(StoreName.Root,
                RuntimeEnvironmentHelper.IsWindows ? StoreLocation.LocalMachine : StoreLocation.CurrentUser))
            {
                AddCertificateToStore(primaryCertificate, store);
                AddCertificateToStore(tsaRootCertificate, store);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                // Act
                var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());

                // Assert
                try
                {
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                    resultsWithWarnings.Count().Should().Be(0);
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message + GetResultIssues(result, dir));
                }
            }
        }

        [Theory]
        [MemberData(nameof(FolderForEachPlatform))]
        public async Task VerifySignaturesAsync_PreGenerateSignedPackages_AuthorSigned_RepositorySigned(string dir)
        {
            // Arrange
            var caseName = "AR";

            var settings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy();

            var signedPackageFolder = Path.Combine(dir, caseName, "package");
            var signedPackagePath = Directory.GetFiles(signedPackageFolder).Where(f => f.EndsWith(".nupkg")).First();

            var certFolder = Path.Combine(dir, caseName, "cert");
            var authorCertFile = Directory.GetFiles(certFolder).Where(f => f.EndsWith("Author.cer")).First();
            var repoCertFile = Directory.GetFiles(certFolder).Where(f => f.EndsWith("Repo.cer")).First();

            using (var primaryCertificate = new X509Certificate2(File.ReadAllBytes(authorCertFile)))
            using (var counterCertificate = new X509Certificate2(File.ReadAllBytes(repoCertFile)))
            using (var packageReader = new PackageArchiveReader(signedPackagePath))
            using (var store = new X509Store(StoreName.Root,
                RuntimeEnvironmentHelper.IsWindows ? StoreLocation.LocalMachine : StoreLocation.CurrentUser))
            {
                AddCertificateToStore(primaryCertificate, store);
                AddCertificateToStore(counterCertificate, store);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                // Act
                var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());

                // Assert
                try
                {
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                    resultsWithWarnings.Count().Should().Be(0);
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message + GetResultIssues(result, dir));
                }
            }
        }

        [Theory]
        [MemberData(nameof(FolderForEachPlatform))]
        public async Task VerifySignaturesAsync_PreGenerateSignedPackages_AuthorSigned_Timestamped_RepositorySigned(string dir)
        {
            // Arrange
            var caseName = "ATR";

            var settings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy();

            var signedPackageFolder = Path.Combine(dir, caseName, "package");
            var signedPackagePath = Directory.GetFiles(signedPackageFolder).Where(f => f.EndsWith(".nupkg")).First();

            var certFolder = Path.Combine(dir, caseName, "cert");
            var authorCertFile = Directory.GetFiles(certFolder).Where(f => f.EndsWith("Author.cer")).First();
            var authorTsaRootCertFile = Directory.GetFiles(certFolder).Where(f => f.EndsWith("AuthorTSARoot.cer")).First();
            var repoCertFile = Directory.GetFiles(certFolder).Where(f => f.EndsWith("Repo.cer")).First();

            using (var primaryCertificate = new X509Certificate2(File.ReadAllBytes(authorCertFile)))
            using (var counterCertificate = new X509Certificate2(File.ReadAllBytes(repoCertFile)))
            using (var tsaRootCertificate = new X509Certificate2(File.ReadAllBytes(authorTsaRootCertFile)))
            using (var packageReader = new PackageArchiveReader(signedPackagePath))
            using (var store = new X509Store(StoreName.Root,
                RuntimeEnvironmentHelper.IsWindows ? StoreLocation.LocalMachine : StoreLocation.CurrentUser))
            {
                AddCertificateToStore(primaryCertificate, store);
                AddCertificateToStore(counterCertificate, store);
                AddCertificateToStore(tsaRootCertificate, store);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                // Act
                var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());

                // Assert
                try
                {
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                    resultsWithWarnings.Count().Should().Be(0);
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message + GetResultIssues(result, dir));
                }
            }
        }

        [Theory]
        [MemberData(nameof(FolderForEachPlatform))]
        public async Task VerifySignaturesAsync_PreGenerateSignedPackages_AuthorSigned_RepositorySigned_Timestamped(string dir)
        {
            // Arrange
            var caseName = "ART";

            var settings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy();

            var signedPackageFolder = Path.Combine(dir, caseName, "package");
            var signedPackagePath = Directory.GetFiles(signedPackageFolder).Where(f => f.EndsWith(".nupkg")).First();

            var certFolder = Path.Combine(dir, caseName, "cert");
            var authorCertFile = Directory.GetFiles(certFolder).Where(f => f.EndsWith("Author.cer")).First();
            var repoCertFile = Directory.GetFiles(certFolder).Where(f => f.EndsWith("Repo.cer")).First();
            var repoTsaRootCertFile = Directory.GetFiles(certFolder).Where(f => f.EndsWith("RepoTSARoot.cer")).First();

            using (var primaryCertificate = new X509Certificate2(File.ReadAllBytes(authorCertFile)))
            using (var counterCertificate = new X509Certificate2(File.ReadAllBytes(repoCertFile)))
            using (var tsaRootCertificate = new X509Certificate2(File.ReadAllBytes(repoTsaRootCertFile)))
            using (var packageReader = new PackageArchiveReader(signedPackagePath))
            using (var store = new X509Store(StoreName.Root,
                RuntimeEnvironmentHelper.IsWindows ? StoreLocation.LocalMachine : StoreLocation.CurrentUser))
            {
                AddCertificateToStore(primaryCertificate, store);
                AddCertificateToStore(counterCertificate, store);
                AddCertificateToStore(tsaRootCertificate, store);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                // Act
                var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());

                // Assert
                try
                {
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                    resultsWithWarnings.Count().Should().Be(0);
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message + GetResultIssues(result, dir));
                }
            }
        }

        [Theory]
        [MemberData(nameof(FolderForWindows_NetFullFramework))]
        public async Task VerifySignaturesAsync_PreGenerateSignedPackages_AuthorSigned_TimeStampedWithNoSigningCertificateUsage_Throws(string dir)
        {
            // Arrange
            string caseName = TestPackages.Package1.ToString();

            var signedPackageFolder = Path.Combine(dir, caseName, "package");
            var signedPackagePath = TestFileSystemUtility.GetFirstFileNameOrNull(signedPackageFolder, "*.nupkg");

            using (FileStream stream = File.OpenRead(signedPackagePath))
            using (var reader = new PackageArchiveReader(stream))
            {
                // Act
                PrimarySignature signature = await reader.GetPrimarySignatureAsync(CancellationToken.None);

                var exception = Assert.Throws<SignatureException>(
                    () => SignatureUtility.GetTimestampCertificateChain(signature));

                Assert.Equal(
                    "Either the signing-certificate or signing-certificate-v2 attribute must be present.",
                    exception.Message);
            }
        }
            
        [Theory]
        [MemberData(nameof(FolderForEachPlatform))]
        public async Task VerifySignaturesAsync_PreGenerateSignedPackages_AuthorSigned_Timestamped_RepositorySigned_Timestamped(string dir)
        {
            // Arrange
            var caseName = "ATRT";

            var settings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy();

            var signedPackageFolder = Path.Combine(dir, caseName, "package");
            var signedPackagePath = Directory.GetFiles(signedPackageFolder).Where(f => f.EndsWith(".nupkg")).First();

            var certFolder = Path.Combine(dir, caseName, "cert");
            var authorCertFile = Directory.GetFiles(certFolder).Where(f => f.EndsWith("Author.cer")).First();
            var authorTsaRootCertFile = Directory.GetFiles(certFolder).Where(f => f.EndsWith("AuthorTSARoot.cer")).First();
            var repoCertFile = Directory.GetFiles(certFolder).Where(f => f.EndsWith("Repo.cer")).First();
            var repoTsaRootCertFile = Directory.GetFiles(certFolder).Where(f => f.EndsWith("RepoTSARoot.cer")).First();

            using (var primaryCertificate = new X509Certificate2(File.ReadAllBytes(authorCertFile)))
            using (var authorTsaRootCertificate = new X509Certificate2(File.ReadAllBytes(authorTsaRootCertFile)))
            using (var counterCertificate = new X509Certificate2(File.ReadAllBytes(repoCertFile)))
            using (var repoTsaRootCertificate = new X509Certificate2(File.ReadAllBytes(repoTsaRootCertFile)))
            using (var packageReader = new PackageArchiveReader(signedPackagePath))
            using (var store = new X509Store(StoreName.Root,
                RuntimeEnvironmentHelper.IsWindows ? StoreLocation.LocalMachine : StoreLocation.CurrentUser))
            {
                AddCertificateToStore(primaryCertificate, store);
                AddCertificateToStore(counterCertificate, store);
                AddCertificateToStore(authorTsaRootCertificate, store);
                AddCertificateToStore(repoTsaRootCertificate, store);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                // Act
                var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());

                // Assert
                try
                {
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                    resultsWithWarnings.Count().Should().Be(0);
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message + GetResultIssues(result, dir));
                }
            }
        }

        private void AddCertificateToStore(X509Certificate2 cert, X509Store store)
        {
            store.Open(OpenFlags.ReadWrite);
            store.Add(cert);

        }

        private string GetResultIssues(VerifySignaturesResult result, string dir)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"verify package from : {dir}");
            int i = 0;
            foreach (var rst in result.Results)
            {
                sb.AppendLine($"result {i}:  {rst.Trust.ToString()}");
                foreach (var error in rst.Issues.Where(issue => issue.Level == LogLevel.Error))
                {
                    sb.AppendLine($"   error :  {error.Code} : {error.Message}");
                }
                foreach (var warning in rst.Issues.Where(issue => issue.Level == LogLevel.Warning))
                {
                    sb.AppendLine($"   warning :  {warning.Code} : {warning.Message}");
                }
                i++;
            }
            return sb.ToString();
        }

        private static string GetGeneratedPackagesRootPath()
        {
            var root = TestFileSystemUtility.NuGetTestFolder;
            var path = Path.Combine(root, TestFolderNames.PreGenPackagesFolder);

            return path;

        }

        public static TheoryData FolderForWindows_NetFullFramework
        {
            get
            {
                var folder = new TheoryData<string>();
                folder.Add(Path.Combine(GetGeneratedPackagesRootPath(), TestFolderNames.Windows_NetFullFrameworkFolder));
                return folder;                
            }
        }

        public static TheoryData FolderForEachPlatform
        {
            get
            {
                /* should have 4 folders:
                    "Windows_NetFulFramework",
                    "Windows_NetCore",
                    "Mac_NetCore",
                    "Linux_NetCore",
                */
                var folders = new TheoryData<string>();
                foreach (string folder in Directory.GetDirectories(GetGeneratedPackagesRootPath()))
                {
                    folders.Add(folder);
                }

                return folders;
            }
        }
    }
}
#endif

