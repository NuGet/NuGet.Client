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
            var zip = nupkg.Create();

            using (var testCert = TestCertificate.Generate().WithTrust())
            using (var signPackage = new SignedPackageArchive(zip))
            {
                var before = new List<string>(zip.Entries.Select(e => e.FullName));

                // Sign the package
                await SignTestUtility.SignPackageAsync(testLogger, testCert.Source.Cert, signPackage);

                // Verify sign file exists
                zip.Entries.Select(e => e.FullName)
                    .Except(before)
                    .Should()
                    .BeEquivalentTo(new[]
                {
                    SigningSpecifications.V1.ManifestPath,
                    SigningSpecifications.V1.SignaturePath1,
                });
            }
        }
    }
}

#endif