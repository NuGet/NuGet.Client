// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using EnvDTE;
using Moq;
using NuGet.VisualStudio;
using NuGetVSExtension;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.VsExtension.Test
{
    // This namespace declaration is on-purpose, to allow for testing the MEF imported VSTS credential provider without actually importing it.
    namespace TeamSystem.NuGetCredentialProvider
    {
        public class VisualStudioAccountProvider : IVsCredentialProvider
        {
            public Task<ICredentials> GetCredentialsAsync(Uri uri, IWebProxy proxy, bool isProxyRequest, bool isRetry, bool nonInteractive, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }

    internal class FailingCredentialProvider : NonFailingCredentialProvider
    {
        public FailingCredentialProvider()
        {
            // simulate a failure at contruction time
            throw new Exception("Exception thrown in constructor of FailingCredentialProvider");
        }
    }

    internal class NonFailingCredentialProvider : IVsCredentialProvider
    {
        public Task<ICredentials> GetCredentialsAsync(Uri uri, IWebProxy proxy, bool isProxyRequest, bool isRetry, bool nonInteractive, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }

    public class VsCredentialProviderImporterTests
    {
        private readonly StringBuilder _testErrorOutput = new StringBuilder();
        private static readonly VisualStudioAccountProvider _visualStudioAccountProvider = new VisualStudioAccountProvider(null, null);
        private readonly Mock<DTE> _mockDte = new Mock<DTE>();
        private readonly Func<Credentials.ICredentialProvider> _fallbackProviderFactory = () => _visualStudioAccountProvider;
        private readonly List<string> _errorMessages = new List<string>();
        private readonly Action<Exception, string> _errorDelegate;

        public VsCredentialProviderImporterTests()
        {
            _errorDelegate = (e, s) => _errorMessages.Add(s);
        }

        private void TestableErrorWriter(string s)
        {
            _testErrorOutput.AppendLine(s);
        }

        private VsCredentialProviderImporter GetTestableImporter()
        {
            var importer = new VsCredentialProviderImporter(
                _mockDte.Object,
                _fallbackProviderFactory,
                _errorDelegate,
                initializer: () => { });

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
            var results = importer.GetProviders();

            // Assert
            Assert.Contains(_visualStudioAccountProvider, results);
        }

        [Fact]
        public void WhenVstsImportNotFound_WhenNotDev14_ThenDoNotInsertBuiltInProvider()
        {
            // Arrange
            _mockDte.Setup(x => x.Version).Returns("15.0.123456.00");
            var importer = GetTestableImporter();

            // Act
            var results = importer.GetProviders();

            // Assert
            Assert.DoesNotContain(_visualStudioAccountProvider, results);
        }

        [Fact]
        public void WhenVstsImportFound_ThenDoNotInsertBuiltInProvider()
        {
            // Arrange
            _mockDte.Setup(x => x.Version).Returns("14.0.247200.00");
            var importer = GetTestableImporter();
            var testableProvider = new TeamSystem.NuGetCredentialProvider.VisualStudioAccountProvider();
            importer.ImportedProviders = new List<Lazy<IVsCredentialProvider>> { new Lazy<IVsCredentialProvider>( () => testableProvider) };

            // Act
            var results = importer.GetProviders();

            // Assert
            Assert.DoesNotContain(_visualStudioAccountProvider, results);
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
                _errorDelegate,
                () => { throw exception; });
            importer.Version = _mockDte.Object.Version;

            // Act & Assert
            var actual = Assert.Throws<ArgumentException>(() => importer.GetProviders());
            Assert.Same(exception, actual);
        }

        [Fact]
        public void WhenImportedProviderFailsOnDev14_ThenOtherProvidersAreStillImportedIncludingBuiltInProvider()
        {
            // Arrange
            _mockDte.Setup(x => x.Version).Returns("14.0.247200.00");
            var importer = GetTestableImporter();
            var nonFailingProviderFactory = new Lazy<IVsCredentialProvider>(() => new NonFailingCredentialProvider());
            var failingProviderFactory = new Lazy<IVsCredentialProvider>(() => new FailingCredentialProvider());
            importer.ImportedProviders = new List<Lazy<IVsCredentialProvider>>
            {
                nonFailingProviderFactory,
                failingProviderFactory
            };

            // Act
            var results = importer.GetProviders();

            // Assert
            // We expect 2 providers:
            // The non-failing provider, and the built-in provider on dev14
            Assert.Equal(2, results.Count);
            Assert.Contains(_visualStudioAccountProvider, results);
        }

        [Fact]
        public void WhenImportedProviderFailsOnDev15_ThenOtherProvidersAreStillImportedExcludingBuiltInProvider()
        {
            // Arrange
            _mockDte.Setup(x => x.Version).Returns("15.0.123456.00");
            var importer = GetTestableImporter();
            var nonFailingProviderFactory = new Lazy<IVsCredentialProvider>(() => new NonFailingCredentialProvider());
            var failingProviderFactory = new Lazy<IVsCredentialProvider>(() => new FailingCredentialProvider());
            importer.ImportedProviders = new List<Lazy<IVsCredentialProvider>>
            {
                nonFailingProviderFactory,
                failingProviderFactory
            };

            // Act
            var results = importer.GetProviders();

            // Assert
            // We expect a single provider:
            // The non-failing provider, and NO built-in provider on dev15
            Assert.Equal(1, results.Count);
            Assert.DoesNotContain(_visualStudioAccountProvider, results);
        }
    }
}
