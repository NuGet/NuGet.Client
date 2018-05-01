// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NuGet.CommandLine.Test;
using NuGet.CommandLine.Test.Caching;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.CommandLine.FuncTest.Commands
{
    /// <summary>
    /// Tests Sign command
    /// These tests require admin privilege as the certs need to be added to the root store location
    /// </summary>
    [Collection(SignCommandTestCollection.Name)]
    public class RestoreCommandSigningTests
    {
        private SignCommandTestFixture _testFixture;
        private readonly TrustedTestCert<TestCertificate> _trustedTestCert;
        private readonly string _nugetExePath;

        public RestoreCommandSigningTests(SignCommandTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
            _nugetExePath = _testFixture.NuGetExePath;
        }


        [CIOnlyFact]
        public async Task Restore_UnsignedPackageFromSourceWithAllSignedTrue_FailsAsync()
        {
            Debugger.Launch();

            // Arrange
            var packageX = new SimpleTestPackageContext("x", "1.0.0");

            using (var preserver = new DefaultConfigurationFilePreserver())
            using (var mockServer = MockServerWithRepositorySignatureinfo.Create())
            using (var pathContext = new SimpleTestPathContext())
            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var certificateFingerprintString = SignatureTestUtility.GetFingerprint(testCertificate, HashAlgorithmName.SHA256);
                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(testCertificate, packageX, dir, new Uri("https://v3serviceIndex.test/api/index.json"));

                var projectA = SimpleTestProjectContext.CreateNETCore(
                        "a",
                        pathContext.SolutionRoot,
                        NuGetFramework.Parse("net45"));

                projectA.AddPackageToAllFrameworks(packageX);
                solution.Projects.Add(projectA);            
                solution.Create(pathContext.SolutionRoot);
                mockServer.Start();

                // Act
                var result = Util.Restore(pathContext, projectA.ProjectPath, 0, "-Source", mockServer.GetV3Source());
                mockServer.Stop();

                // Assert
            }
        }

        private class MockServerWithRepositorySignatureinfo : MockServer
        {
            private MockResponseBuilder _builder;

            public static MockServerWithRepositorySignatureinfo Create()
            {
                var mockServer = new MockServerWithRepositorySignatureinfo();
                var baseUrl = mockServer.Uri.TrimEnd(new[] { '/' });
                mockServer._builder = new MockResponseBuilder(baseUrl);

                mockServer.Get.Add(
                    mockServer._builder.GetV3IndexPath(),
                    request =>
                    {
                        return new Action<HttpListenerResponse>(response =>
                        {
                            var mockResponse = mockServer._builder.BuildV3IndexWithRepoSignResponse(mockServer);
                            response.ContentType = mockResponse.ContentType;
                            SetResponseContent(response, mockResponse.Content);
                        });
                    });

                mockServer.Get.Add(
                    mockServer._builder.GetRepoSignIndexPath(),
                    request =>
                    {
                        return new Action<HttpListenerResponse>(response =>
                        {
                            var mockResponse = mockServer._builder.BuildRepoSignIndexResponse();
                            response.ContentType = mockResponse.ContentType;
                            SetResponseContent(response, mockResponse.Content);
                        });
                    });

                return mockServer;
            }

            public string GetV3Source()
            {
                return _builder.GetV3Source();
            }
        }
    }
}
