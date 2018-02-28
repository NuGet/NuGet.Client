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
    public class SigningOptionsTests
    {
        [Fact]
        public void Constructor_WhenPackagePathIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new SigningOptions(packageFilePath: null,
                                        outputFilePath: "outputPath",
                                        overwrite: true,
                                        signatureProvider: Mock.Of<ISignatureProvider>(),
                                        logger: Mock.Of<ILogger>()));

            Assert.Equal("packageFilePath", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenOutputPathIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new SigningOptions(packageFilePath: "packagePath",
                                        outputFilePath: null,
                                        overwrite: true,
                                        signatureProvider: Mock.Of<ISignatureProvider>(),
                                        logger: Mock.Of<ILogger>()));

            Assert.Equal("outputFilePath", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenOutputPathAndPackagePathAreEqual_Throws()
        {
            var authorSignPackageRequest = new AuthorSignPackageRequest(new X509Certificate2(), HashAlgorithmName.SHA256);
            var exception = Assert.Throws<ArgumentException>(
                () => new SigningOptions(packageFilePath: "packagePath",
                                        outputFilePath: "packagePath",
                                        overwrite: true,
                                        signatureProvider: Mock.Of<ISignatureProvider>(),
                                        logger: Mock.Of<ILogger>()));

            Assert.Equal("packageFilePath and outputFilePath should be different. Package signing cannot be done in place.", exception.Message);
        }

        [Fact]
        public void Constructor_WhenSignatureProviderIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new SigningOptions(packageFilePath: "packagePath",
                                        outputFilePath: "outputPath",
                                        overwrite: true,
                                        signatureProvider: null,
                                        logger: Mock.Of<ILogger>()));

            Assert.Equal("signatureProvider", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenLoggerIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new SigningOptions(packageFilePath: "packagePath",
                                        outputFilePath: "outputPath",
                                        overwrite: true,
                                        signatureProvider: Mock.Of<ISignatureProvider>(),
                                        logger: null));

            Assert.Equal("logger", exception.ParamName);
        }
    }
}
#endif