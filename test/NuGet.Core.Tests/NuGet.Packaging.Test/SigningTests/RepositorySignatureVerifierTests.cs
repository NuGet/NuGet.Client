// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_DESKTOP

using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Common;
using NuGet.Packaging.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class RepositorySignatureVerifierTests
    {
        private readonly Mock<ISignedPackageReader> _packageReader;
        private readonly RepositorySignatureVerifier _verifier;

        public RepositorySignatureVerifierTests()
        {
            _packageReader = new Mock<ISignedPackageReader>(MockBehavior.Strict);
            _verifier = new RepositorySignatureVerifier();
        }

        [Fact]
        public async Task VerifyAsync_WhenReaderIsNull_Throws()
        {
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => _verifier.VerifyAsync(reader: null, cancellationToken: CancellationToken.None));

            Assert.Equal("reader", exception.ParamName);
        }

        [Fact]
        public Task VerifyAsync_WhenCancellationTokenIsCancelled_Throws()
        {
            return Assert.ThrowsAsync<OperationCanceledException>(
                () => _verifier.VerifyAsync(_packageReader.Object, new CancellationToken(canceled: true)));
        }

        [Fact]
        public async Task VerifyAsync_WhenPackageIsNotSigned_Throws()
        {
            _packageReader.Setup(x => x.IsSignedAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var exception = await Assert.ThrowsAsync<SignatureException>(
                () => _verifier.VerifyAsync(_packageReader.Object, CancellationToken.None));

            Assert.Equal(NuGetLogCode.NU3000, exception.Code);
            Assert.Equal("The package is not signed. Unable to verify signature from an unsigned package.", exception.Message);
        }
    }
}
#endif