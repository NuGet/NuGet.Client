// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Packaging.Test.SigningTests
{
    public class SignerTests
    {
        [Fact]
        public async Task Signer_CreateSignedPackage()
        {
            var nupkg = new SimpleTestPackageContext();
            var testLogger = new TestLogger();
            var zip = nupkg.Create();

            using (var signPackage = new SignedPackageArchive(zip))
            {
                var before = new List<string>(zip.Entries.Select(e => e.FullName));
                var signer = new Signer(signPackage);
                var signature = new Signature()
                {
                    DisplayName = "Test signer",
                    TestTrust = SignatureVerificationStatus.Trusted,
                    Type = SignatureType.Author
                };

                var request = new SignPackageRequest()
                {
                    Signature = signature
                };

                await signer.SignAsync(request, testLogger, CancellationToken.None);

                // Verify sign file exists
                zip.Entries.Select(e => e.FullName)
                    .Except(before)
                    .Should()
                    .BeEquivalentTo(new[]
                {
                    "testsigned/signed.json"
                });
            }
        }

        [Fact]
        public async Task Signer_CreateSignedPackageAndRemoveSignatureVerifyFileRemoved()
        {
            var nupkg = new SimpleTestPackageContext();
            var testLogger = new TestLogger();
            var zip = nupkg.Create();

            using (var signPackage = new SignedPackageArchive(zip))
            {
                var before = new List<string>(zip.Entries.Select(e => e.FullName));
                var signer = new Signer(signPackage);
                var signature = new Signature()
                {
                    DisplayName = "Test signer",
                    TestTrust = SignatureVerificationStatus.Trusted,
                    Type = SignatureType.Author
                };

                var request = new SignPackageRequest()
                {
                    Signature = signature
                };

                await signer.SignAsync(request, testLogger, CancellationToken.None);

                await signer.RemoveSignatureAsync(signature, testLogger, CancellationToken.None);

                // Verify sign file exists
                zip.Entries.Select(e => e.FullName)
                    .Except(before)
                    .Should()
                    .BeEmpty();
            }
        }

        [Fact]
        public async Task Signer_CreateSignedPackageAndRemoveAllSignaturesVerifyFileRemoved()
        {
            var nupkg = new SimpleTestPackageContext();
            var testLogger = new TestLogger();
            var zip = nupkg.Create();

            using (var signPackage = new SignedPackageArchive(zip))
            {
                var before = new List<string>(zip.Entries.Select(e => e.FullName));
                var signer = new Signer(signPackage);
                var signature = new Signature()
                {
                    DisplayName = "Test signer",
                    TestTrust = SignatureVerificationStatus.Trusted,
                    Type = SignatureType.Author
                };

                var request = new SignPackageRequest()
                {
                    Signature = signature
                };

                await signer.SignAsync(request, testLogger, CancellationToken.None);

                await signer.RemoveSignaturesAsync(testLogger, CancellationToken.None);

                // Verify sign file exists
                zip.Entries.Select(e => e.FullName)
                    .Except(before)
                    .Should()
                    .BeEmpty();
            }
        }
    }
}