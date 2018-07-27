// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using EnvDTE;
using Moq;
using NuGet.VisualStudio;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.VisualStudio.Test
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

    internal class ThirdPartyCredentialProvider : IVsCredentialProvider
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
                _errorDelegate,
                initializer: () => { });

            return importer;
        }
        
        [Fact]
        public void WhenMultipleProvidersMatchingVstsContractFound_ThenInsertAll()
        {
            // Arrange
            // This simulates the fact that a third-party credential provider could export using the same 
            // contract name as the VisualStudioAccountProvider from TeamExplorer.
            // When this happens, both should just happily load.
            var importer = GetTestableImporter();
            var testableProvider = new TeamSystem.NuGetCredentialProvider.VisualStudioAccountProvider();
            importer.VisualStudioAccountProviders = new List<Lazy<IVsCredentialProvider>>
            {
                new Lazy<IVsCredentialProvider>(() => testableProvider),
                new Lazy<IVsCredentialProvider>(() => new NonFailingCredentialProvider())
            };

            // Act
            var results = importer.GetProviders();

            // Assert
            // We expect 2 providers:
            Assert.Equal(2, results.Count);
            Assert.DoesNotContain(_visualStudioAccountProvider, results);
        }

        [Fact]
        public void ImportsAllFoundProviders()
        {
            // Arrange
            // This test verifies the scenario where multiple credential providers are found, both third-party,
            // as well as matching the contract name of the "VisualStudioAccountProvider".
            // All of them should just happily import.
            var importer = GetTestableImporter();
            var testableProvider = new TeamSystem.NuGetCredentialProvider.VisualStudioAccountProvider();
            importer.VisualStudioAccountProviders = new List<Lazy<IVsCredentialProvider>>
            {
                new Lazy<IVsCredentialProvider>(() => testableProvider),
                new Lazy<IVsCredentialProvider>(() => new NonFailingCredentialProvider()),
                // This one will not be imported
                new Lazy<IVsCredentialProvider>(() => new FailingCredentialProvider())
            };
            importer.ImportedProviders = new List<Lazy<IVsCredentialProvider>>
            {
                new Lazy<IVsCredentialProvider>(() => new ThirdPartyCredentialProvider()),
                // This one will not be imported
                new Lazy<IVsCredentialProvider>(() => new FailingCredentialProvider())
            };

            // Act
            var results = importer.GetProviders();

            // Assert
            // We expect 3 providers:
            // The 2 proviers matching the "VisualStudioAccountProvider" contract name,
            // and the "third party" provider.
            // The "failing" credential providers will not be imported, as they are failing :)
            Assert.Equal(3, results.Count);
            Assert.DoesNotContain(_visualStudioAccountProvider, results);
        }

        [Fact]
        public void WhenVstsIntializerThrows_ThenExceptionBubblesOut()
        {
            // Arrange
            var exception = new ArgumentException();
            var importer = new VsCredentialProviderImporter(
                _errorDelegate,
                () => { throw exception; });

            // Act & Assert
            var actual = Assert.Throws<ArgumentException>(() => importer.GetProviders());
            Assert.Same(exception, actual);
        }

        [Fact]
        public void WhenImportedProviderFailsOnDev15_ThenOtherProvidersAreStillImportedExcludingBuiltInProvider()
        {
            // Arrange
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
