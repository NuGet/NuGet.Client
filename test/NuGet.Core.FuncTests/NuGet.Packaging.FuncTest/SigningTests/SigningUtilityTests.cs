// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP

using System;
using System.Security.Cryptography.X509Certificates;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Packaging.FuncTest
{
    [Collection("Signing Functional Test Collection")]
    public class SigningUtilityTests
    {
        private readonly SigningTestFixture _fixture;

        public SigningUtilityTests(SigningTestFixture fixture)
        {
            if (fixture == null)
            {
                throw new ArgumentNullException(nameof(fixture));
            }

            _fixture = fixture;
        }

        [CIOnlyFact]
        public void Verify_WithValidInput_DoesNotThrow()
        {
            using (var certificate = new X509Certificate2(_fixture.TrustedTestCertificate.Source.PublicCert.RawData))
            using (var request = new SignPackageRequest(certificate, HashAlgorithmName.SHA256, HashAlgorithmName.SHA256))
            {
                SigningUtility.Verify(request, NullLogger.Instance);
            }
        }
    }
}
#endif