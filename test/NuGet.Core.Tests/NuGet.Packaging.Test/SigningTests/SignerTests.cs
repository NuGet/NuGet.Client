// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

            using (var signPackage = new SignPackageArchive(zip))
            {
                var before = new List<string>(zip.Entries.Select(e => e.FullName));
                var signer = new Signer(signPackage);
                var signature = new Signature();

                await signer.SignAsync(signature, testLogger, CancellationToken.None);

                zip.Entries.Select(e => e.FullName)
                    .Except(before)
                    .Should()
                    .BeEquivalentTo(new[]
                {
                    "testsigned/signed.json"
                });
            }
        }
    }
}
