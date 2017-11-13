// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Threading;
using FluentAssertions;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.FuncTest
{
    public class TimestampProviderTests
    {
        private const string _internalTimestamper = "http://rfc3161.gtm.corp.microsoft.com/TSS/HttpTspServer ";

        [Fact]
        public void Rfc3161TimestampProvider_Success()
        {

            // Arrange
            var logger = new TestLogger();
            var timestampProvider = new Rfc3161TimestampProvider(new Uri(_internalTimestamper));
            var authorCertName = "author@nuget.func.test";
            var data = "Test data to be signed and timestamped";

            using (var authorCert = SigningTestUtility.GenerateCertificate(authorCertName, modifyGenerator: null))
            {
                var signedCms = SigningTestUtility.GenerateSignedCms(authorCert, Encoding.ASCII.GetBytes(data));
                var signatureValue = signedCms.Encode();

                var request = new TimestampRequest
                {
                    Certificate = authorCert,
                    SigningSpec = SigningSpecifications.V1,
                    TimestampHashAlgorithm = Common.HashAlgorithmName.SHA256,
                    SignatureValue = signatureValue
                };

                // Act
                var timestampedData = timestampProvider.TimestampData(request, logger, CancellationToken.None);
                var timestampedCms = new SignedCms();
                timestampedCms.Decode(timestampedData);

                // Assert
                Assert.NotNull(timestampedData);
                Assert.NotNull(timestampedCms);
            }
        }
    }
}
