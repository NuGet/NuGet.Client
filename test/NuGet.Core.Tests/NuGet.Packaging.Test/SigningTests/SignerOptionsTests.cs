// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if NET46
using System;
using System.Security.Cryptography.X509Certificates;
using Moq;
using NuGet.Common;
using NuGet.Packaging.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class SignerOptionsTests
    {
        [Fact]
        public void Constructor_WhenPackagePathIsNull_Throws()
        {
            var authorSignPackageRequest = new AuthorSignPackageRequest(new X509Certificate2(), HashAlgorithmName.SHA256);
            var exception = Assert.Throws<ArgumentNullException>(
                () => new SignerOptions(packagePath: null,
                                        outputPath: "outputPath",
                                        overwrite: true,
                                        signatureProvider: Mock.Of<ISignatureProvider>(),
                                        signRequest: authorSignPackageRequest,
                                        logger: Mock.Of<ILogger>()));

            Assert.Equal("packagePath", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenOutputPathIsNull_Throws()
        {
            var authorSignPackageRequest = new AuthorSignPackageRequest(new X509Certificate2(), HashAlgorithmName.SHA256);
            var exception = Assert.Throws<ArgumentNullException>(
                () => new SignerOptions(packagePath: "packagePath",
                                        outputPath: null,
                                        overwrite: true,
                                        signatureProvider: Mock.Of<ISignatureProvider>(),
                                        signRequest: authorSignPackageRequest,
                                        logger: Mock.Of<ILogger>()));

            Assert.Equal("outputPath", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenOutputPathAndPackagePathAreEqual_Throws()
        {
            var authorSignPackageRequest = new AuthorSignPackageRequest(new X509Certificate2(), HashAlgorithmName.SHA256);
            var exception = Assert.Throws<ArgumentException>(
                () => new SignerOptions(packagePath: "packagePath",
                                        outputPath: "packagePath",
                                        overwrite: true,
                                        signatureProvider: Mock.Of<ISignatureProvider>(),
                                        signRequest: authorSignPackageRequest,
                                        logger: Mock.Of<ILogger>()));

            Assert.Equal("PackageFilePath and OutputFilePath should be different. Package signing cannot be done in place.", exception.Message);
        }

        [Fact]
        public void Constructor_WhenSignatureProviderIsNull_Throws()
        {
            var authorSignPackageRequest = new AuthorSignPackageRequest(new X509Certificate2(), HashAlgorithmName.SHA256);
            var exception = Assert.Throws<ArgumentNullException>(
                () => new SignerOptions(packagePath: "packagePath",
                                        outputPath: "outputPath",
                                        overwrite: true,
                                        signatureProvider: null,
                                        signRequest: authorSignPackageRequest,
                                        logger: Mock.Of<ILogger>()));

            Assert.Equal("signatureProvider", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenSignRequestIsNull_Throws()
        {
            var authorSignPackageRequest = new AuthorSignPackageRequest(new X509Certificate2(), HashAlgorithmName.SHA256);
            var exception = Assert.Throws<ArgumentNullException>(
                () => new SignerOptions(packagePath: "packagePath",
                                        outputPath: "outputPath",
                                        overwrite: true,
                                        signatureProvider: Mock.Of<ISignatureProvider>(),
                                        signRequest: null,
                                        logger: Mock.Of<ILogger>()));

            Assert.Equal("signRequest", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenLoggerIsNull_Throws()
        {
            var authorSignPackageRequest = new AuthorSignPackageRequest(new X509Certificate2(), HashAlgorithmName.SHA256);
            var exception = Assert.Throws<ArgumentNullException>(
                () => new SignerOptions(packagePath: "packagePath",
                                        outputPath: "outputPath",
                                        overwrite: true,
                                        signatureProvider: Mock.Of<ISignatureProvider>(),
                                        signRequest: authorSignPackageRequest,
                                        logger: null));

            Assert.Equal("logger", exception.ParamName);
        }
    }
}
#endif