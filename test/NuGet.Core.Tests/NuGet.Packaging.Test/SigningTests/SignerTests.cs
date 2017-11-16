// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility.Signing;
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
            var zipReadStream = nupkg.CreateAsStream();
            var zipWriteStream = nupkg.CreateAsStream();

            using (var testCert = TestCertificate.Generate().WithTrust())
            using (var signPackage = new SignedPackageArchive(zipReadStream, zipWriteStream))
            {
                // Sign the package
                await SignTestUtility.SignPackageAsync(testLogger, testCert.Source.Cert, signPackage);

                var settings = SignedPackageVerifierSettings.RequireSigned;

                var result = await SignTestUtility.VerifySignatureAsync(testLogger, signPackage, settings);

                result.Valid.Should().BeTrue();
            }

        }
    }
}

#endif