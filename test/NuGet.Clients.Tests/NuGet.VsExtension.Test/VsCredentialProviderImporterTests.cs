// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Moq;
using NuGet.VisualStudio;
using NuGetVSExtension;
using Xunit;

namespace NuGet.VsExtension.Test
{
    namespace TeamSystem.NuGetCredentialProvider
    {
        public class VisualStudioAccountProvider : IVsCredentialProvider
        {
            public Task<ICredentials> GetCredentialsAsync(Uri uri, IWebProxy proxy, bool isProxyRequest, bool isRetry, bool nonInteractive,
                CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }

    public class VsCredentialProviderImporterTests
    {
        private readonly Mock<DTE> _mockDte = new Mock<DTE>();
        private readonly Func<Credentials.ICredentialProvider> _fallbackProviderFactory = () => new VisualStudioAccountProvider(null, null);
        private readonly List<string> _errorMessages = new List<string>();
        private readonly Action<string> _errorDelegate;

        public VsCredentialProviderImporterTests()
        {
            _errorDelegate = s => _errorMessages.Add(s);
        }

        private VsCredentialProviderImporter GetTestableImporter()
        {
            var importer = new VsCredentialProviderImporter(
                _mockDte.Object,
                _fallbackProviderFactory,
                _errorDelegate,
                () => { });
            importer.Version = _mockDte.Object.Version;
            return importer;
        }

        [Fact]
        public void WhenVstsImportNotFound_WhenDev14_ThenInsertBuiltInProvider()
        {
            _mockDte.Setup(x => x.Version).Returns("14.0.247200.00");
            var importer = GetTestableImporter();

            var result = importer.GetProvider();

            Assert.IsType<VisualStudioAccountProvider>(result);
        }

        [Fact]
        public void WhenVstsImportNotFound_WhenNotDev14_ThenDoNotInsertBuiltInProvider()
        {
            _mockDte.Setup(x => x.Version).Returns("15.0.123456.00");
            var importer = GetTestableImporter();

            var result = importer.GetProvider();

            Assert.Null(result);
        }

        [Fact]
        public void WhenVstsImportFound_ThenDoNotInsertBuiltInProvider()
            {
            _mockDte.Setup(x => x.Version).Returns("14.0.247200.00");
            var importer = GetTestableImporter();
            var testableProvider = new TeamSystem.NuGetCredentialProvider.VisualStudioAccountProvider();
            importer.ImportedProvider = testableProvider;

            var result = importer.GetProvider();

            Assert.IsType<VsCredentialProviderAdapter>(result);
        }

        [Fact]
        public void WhenVstsIntializerThrows_ThenGetProviderReturnsNull()
        {
            _mockDte.Setup(x => x.Version).Returns("14.0.247200.00");
            var importer = new VsCredentialProviderImporter(
                _mockDte.Object,
                _fallbackProviderFactory,
                _errorDelegate,
                () => { throw new ArgumentException(); });
            importer.Version = _mockDte.Object.Version;

            var result = importer.GetProvider();

            Assert.Null(result);
        }
    }
}
