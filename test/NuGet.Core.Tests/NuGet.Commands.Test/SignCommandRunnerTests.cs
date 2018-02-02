// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Commands.Test
{
    public class SignCommandRunnerTests : IClassFixture<CertificatesFixture>
    {
        private readonly CertificatesFixture _fixture;

        public SignCommandRunnerTests(CertificatesFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ExecuteCommandAsync_WithCertificateFileNotFound_Throws()
        {
            using (var test = Test.Create(_fixture.GetDefaultCertificate()))
            {
                var certificateFilePath = Path.Combine(test.Directory.Path, "certificate.pfx");

                test.Args.CertificatePath = certificateFilePath;

                var exception = await Assert.ThrowsAsync<SignCommandException>(
                    () => test.Runner.ExecuteCommandAsync(test.Args));

                Assert.Equal(NuGetLogCode.NU3001, exception.AsLogMessage().Code);
                Assert.Equal($"Certificate file '{certificateFilePath}' not found. For a list of accepted ways to provide a certificate, please visit https://docs.nuget.org/docs/reference/command-line-reference", exception.Message);
            }
        }

        [Fact]
        public async Task ExecuteCommandAsync_WithEmptyPkcs7File_Throws()
        {
            using (var test = Test.Create(_fixture.GetDefaultCertificate()))
            {
                const string fileName = "EmptyCertificateStore.p7b";
                var certificateFilePath = Path.Combine(test.Directory.Path, fileName);

                // This resource was created by calling X509Certificate2Collection.Export(X509ContentType.SerializedStore)
                // with no certificates in the collection.  Programmatic creation works fine on Windows but not under Mono,
                // hence the static resource.
                var bytes = GetResource(fileName);

                File.WriteAllBytes(certificateFilePath, bytes);

                test.Args.CertificatePath = certificateFilePath;

                var exception = await Assert.ThrowsAsync<SignCommandException>(
                    () => test.Runner.ExecuteCommandAsync(test.Args));

                Assert.Equal(NuGetLogCode.NU3001, exception.AsLogMessage().Code);
                Assert.Equal($"Certificate file '{certificateFilePath}' is invalid. For a list of accepted ways to provide a certificate, please visit https://docs.nuget.org/docs/reference/command-line-reference", exception.Message);
            }
        }

        [Fact]
        public async Task ExecuteCommandAsync_WithNoCertificateFound_Throws()
        {
            using (var test = Test.Create(_fixture.GetDefaultCertificate()))
            {
                test.Args.CertificateFingerprint = "invalid fingerprint";
                test.Args.CertificateStoreLocation = StoreLocation.CurrentUser;
                test.Args.CertificateStoreName = StoreName.My;

                var exception = await Assert.ThrowsAsync<SignCommandException>(
                    () => test.Runner.ExecuteCommandAsync(test.Args));

                Assert.Equal(NuGetLogCode.NU3001, exception.AsLogMessage().Code);
                Assert.Equal("No certificates were found that meet all the given criteria. For a list of accepted ways to provide a certificate, please visit https://docs.nuget.org/docs/reference/command-line-reference", exception.Message);
            }
        }

        [Fact]
        public async Task ExecuteCommandAsync_WithIncorrectPassword_Throws()
        {
            const string password = "password";

            using (var test = Test.Create(_fixture.GetCertificateWithPassword(password)))
            {
                var certificateFilePath = Path.Combine(test.Directory.Path, "certificate.pfx");

                File.WriteAllBytes(certificateFilePath, test.Certificate.Export(X509ContentType.Pkcs12, password));

                test.Args.CertificatePath = certificateFilePath;
                test.Args.CertificatePassword = "incorrect password";

                var exception = await Assert.ThrowsAsync<SignCommandException>(
                    () => test.Runner.ExecuteCommandAsync(test.Args));

                Assert.Equal(NuGetLogCode.NU3001, exception.AsLogMessage().Code);
                Assert.Equal($"Invalid password was provided for the certificate file '{certificateFilePath}'. Please provide a valid password using the '-CertificatePassword' option", exception.Message);
            }
        }

        [Fact]
        public async Task ExecuteCommandAsync_WithAmbiguousMatch_Throws()
        {
            using (var test = Test.Create(_fixture.GetDefaultCertificate()))
            {
                test.Args.CertificateSubjectName = "Root";
                test.Args.CertificateStoreLocation = StoreLocation.LocalMachine;
                test.Args.CertificateStoreName = StoreName.Root;

                var exception = await Assert.ThrowsAsync<SignCommandException>(
                    () => test.Runner.ExecuteCommandAsync(test.Args));

                Assert.Equal(NuGetLogCode.NU3001, exception.AsLogMessage().Code);
                Assert.Equal("Multiple certificates were found that meet all the given criteria. Use the '-CertificateFingerprint' option with the hash of the desired certificate.", exception.Message);
            }
        }

        [Fact]
        public async Task ExecuteCommandAsync_WithMultiplePackagesAndInvalidCertificate_RaisesErrorsOnce()
        {
            const string password = "password";

            using (var test = Test.Create(_fixture.GetCertificateWithPassword(password)))
            {
                var certificateFilePath = Path.Combine(test.Directory.Path, "certificate.pfx");

                File.WriteAllBytes(certificateFilePath, test.Certificate.Export(X509ContentType.Pkcs12, password));

                test.Args.CertificatePath = certificateFilePath;
                test.Args.CertificatePassword = password;

                Test.CreatePackage(test.Directory, "package2.nupkg");

                var packagesFilePath = Path.Combine(test.Directory, "*.nupkg");

                test.Args.PackagePath = packagesFilePath;

                await test.Runner.ExecuteCommandAsync(test.Args);

                Assert.Equal(1, test.Logger.LogMessages.Count(
                    message => message.Level == LogLevel.Warning && message.Code == NuGetLogCode.NU3018));
            }
        }

        private static byte[] GetResource(string name)
        {
            return ResourceTestUtility.GetResourceBytes(
                $"NuGet.Commands.Test.compiler.resources.{name}",
                typeof(SignCommandRunnerTests));
        }

        private sealed class Test : IDisposable
        {
            private bool _isDisposed;

            internal SignArgs Args { get; }
            internal X509Certificate2 Certificate { get; }
            internal TestDirectory Directory { get; }
            internal TestLogger Logger { get; }
            internal SignCommandRunner Runner { get; }

            internal Test(SignArgs args, TestDirectory directory, X509Certificate2 certificate, TestLogger logger)
            {
                Args = args;
                Directory = directory;
                Certificate = certificate;
                Runner = new SignCommandRunner();
                Logger = logger;
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    Certificate.Dispose();

                    GC.SuppressFinalize(this);

                    _isDisposed = true;
                }
            }

            internal static Test Create(X509Certificate2 certificate)
            {
                var directory = TestDirectory.Create();
                var packageFilePath = CreatePackage(directory, "package.nupkg");
                var logger = new TestLogger();

                var args = new SignArgs()
                {
                    Logger = logger,
                    NonInteractive = true,
                    PackagePath = packageFilePath
                };

                return new Test(args, directory, certificate, logger);
            }

            internal static string CreatePackage(TestDirectory directory, string packageFileName)
            {
                var packageFilePath = Path.Combine(directory.Path, packageFileName);

                using (var readStream = new SimpleTestPackageContext().CreateAsStream())
                using (var writeStream = File.OpenWrite(packageFilePath))
                {
                    readStream.CopyTo(writeStream);
                }

                return packageFilePath;
            }
        }
    }
}