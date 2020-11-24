// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace Dotnet.Integration.Test
{
    [Collection("Dotnet Integration Tests")]
    public class DotnetSignTests : IClassFixture<SignCommandTestFixture>
    {
        private MsbuildIntegrationTestFixture _msbuildFixture;
        private SignCommandTestFixture _signFixture;

        private const string _packageAlreadySignedError = "NU3001: The package already contains a signature. Please remove the existing signature before adding a new signature.";
        private readonly string _invalidPasswordErrorCode = NuGetLogCode.NU3001.ToString();
        private readonly string _chainBuildFailureErrorCode = NuGetLogCode.NU3018.ToString();
        private readonly string _noCertFoundErrorCode = NuGetLogCode.NU3001.ToString();
        private readonly string _noTimestamperWarningCode = NuGetLogCode.NU3002.ToString();
        private readonly string _timestampUnsupportedDigestAlgorithmCode = NuGetLogCode.NU3024.ToString();

        private TrustedTestCert<TestCertificate> _trustedTestCert;

        public DotnetSignTests(MsbuildIntegrationTestFixture buildFixture, SignCommandTestFixture signFixture)
        {
            _msbuildFixture = buildFixture;
            _signFixture = signFixture;
            _trustedTestCert = signFixture.TrustedTestCertificate;
        }

        [Fact]
        public async Task Sign_SignPackageWithTrustedCertificate_Succceeds()
        {
            // Arrange
            var package = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var zipStream = await package.CreateAsStreamAsync())
            {
                var packagePath = Path.Combine(dir, Guid.NewGuid().ToString());

                zipStream.Seek(offset: 0, loc: SeekOrigin.Begin);

                using (var fileStream = File.OpenWrite(packagePath))
                {
                    zipStream.CopyTo(fileStream);
                }

                //Act
                var result = _msbuildFixture.RunDotnet(
                    dir,
                    $"nuget sign {packagePath} --certificate-fingerprint {_trustedTestCert.Source.Cert.Thumbprint} --certificate-store-name {_trustedTestCert.StoreName} --certificate-store-location {_trustedTestCert.StoreLocation}",
                    ignoreExitCode: true);

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                result.AllOutput.Should().Contain(_noTimestamperWarningCode);
            }
        }
    }
}
