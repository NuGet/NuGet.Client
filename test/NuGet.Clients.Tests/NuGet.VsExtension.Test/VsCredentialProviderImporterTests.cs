// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using EnvDTE;
using Moq;
using NuGet.VisualStudio;
using NuGetVSExtension;
using Xunit;

namespace NuGet.VsExtension.Test
{
    public class VsCredentialProviderImporterTests
    {
        private readonly Mock<DTE> _mockDte = new Mock<DTE>();
        private readonly Func<Credentials.ICredentialProvider> _fallbackProviderFactory = () => new VisualStudioAccountProvider(null, null);

        private VsCredentialProviderImporter GetTestableImporter()
        {
            var importer = new VsCredentialProviderImporter(
                _mockDte.Object,
                _fallbackProviderFactory,
                () => { });

            importer.Version = _mockDte.Object.Version;

            return importer;
        }

        [Fact]
        public void WhenVstsImportNotFound_WhenDev14_ThenInsertBuiltInProvider()
        {
            // Arrange
            _mockDte.Setup(x => x.Version).Returns("14.0.247200.00");
            var importer = GetTestableImporter();

            // Act
            var result = importer.GetProvider();

            // Assert
            Assert.IsType<VisualStudioAccountProvider>(result);
        }

        [Fact]
        public void WhenVstsImportNotFound_WhenNotDev14_ThenDoNotInsertBuiltInProvider()
        {
            // Arrange
            _mockDte.Setup(x => x.Version).Returns("15.0.123456.00");
            var importer = GetTestableImporter();

            // Act
            var result = importer.GetProvider();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void WhenVstsImportFound_ThenDoNotInsertBuiltInProvider()
        {
            // Arrange
            _mockDte.Setup(x => x.Version).Returns("14.0.247200.00");
            var importer = GetTestableImporter();
            var provider = new Mock<IVsCredentialProvider>();
            var testableProvider = provider.Object;
            importer.ImportedProvider = testableProvider;

            // Act
            var result = importer.GetProvider();

            // Assert
            Assert.IsType<VsCredentialProviderAdapter>(result);
        }

        [Fact]
        public void WhenVstsIntializerThrows_ThenExceptionBubblesOut()
        {
            // Arrange
            var exception = new ArgumentException();
            _mockDte.Setup(x => x.Version).Returns("14.0.247200.00");
            var importer = new VsCredentialProviderImporter(
                _mockDte.Object,
                _fallbackProviderFactory,
                () => { throw exception; });
            importer.Version = _mockDte.Object.Version;

            // Act & Assert
            var actual = Assert.Throws<ArgumentException>(() => importer.GetProvider());
            Assert.Same(exception, actual);
        }
    }
}
