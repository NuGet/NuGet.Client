// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.FuncTest
{
    public class SignerTests
    {

        private static readonly IList<ISignatureVerificationProvider> _trustProviders = new List<ISignatureVerificationProvider>()
        {
            new X509SignatureVerificationProvider(),
            new NuGetIntegrityVerificationProvider(),
            new TimestampVerificationProvider()
        };

        private static readonly SigningSpecifications _signingSpecifications = SigningSpecifications.V1;

        [Fact]
        public async Task Signer_SignPackageAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();
            var testLogger = new TestLogger();

            using (var testCert = TestCertificate.Generate().WithTrust())         
            {
                // Act
                var signedPackagePath = await SignedArchiveTestUtility.CreateSignedPackageAsync(testCert, nupkg);

                // Assert
                using (var stream = File.OpenRead(signedPackagePath))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    zip.GetEntry(_signingSpecifications.SignaturePath).Should().NotBeNull();
                }
            }
        }
    }
}

#endif